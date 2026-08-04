# FluxoCaixa — Lançamentos e Consolidado

Dois serviços independentes. **Lançamentos** registra créditos e débitos do caixa de um
comerciante: é a fonte da verdade financeira do sistema, o histórico é imutável, e nenhuma outra
operação além do registro é exposta (não há leitura, alteração, exclusão nem estorno).
**Consolidado** consome o evento publicado pelo Lançamentos e expõe o saldo diário resultante —
total de créditos, total de débitos e saldo líquido de uma data.

O desenho é governado por uma assimetria deliberada: **registrar é crítico, consultar é
degradável**. O Lançamentos confirma o registro sem depender do transporte de mensagens
(RabbitMQ) nem de qualquer consumidor a jusante — a propagação do evento acontece fora do caminho
da requisição, via padrão outbox transacional. O Consolidado, do outro lado dessa mesma
assimetria, admite consistência eventual, uma defasagem de poucos segundos e disponibilidade
menor: sua indisponibilidade nunca afeta o registro.

## Arquitetura

**Lançamentos** — Clean Architecture / Hexagonal, com quatro projetos na raiz da solução,
dependência sempre apontando para dentro:

```
Api ──────────▶ Aplicacao ──────────▶ Dominio
 │                  ▲                    ▲
 └──▶ Infraestrutura ┘────────────────────┘
      (implementa as portas declaradas em Aplicacao)
```

- **Dominio** — entidade `Lancamento` com invariantes no construtor e value objects (`Dinheiro`,
  `DataCompetencia`, `Descricao`, `TipoLancamento`, `ComercianteId`). Sem dependência externa.
- **Aplicacao** — caso de uso `RegistrarLancamento` e as portas que a infraestrutura implementa.
- **Infraestrutura** — persistência em PostgreSQL via EF Core, publicação via MassTransit sobre
  RabbitMQ com outbox transacional, expurgo em segundo plano.
- **Api** — endpoint HTTP mínimo, autenticação, limite de taxa, limite de corpo, tradução de erro
  de domínio para Problem Details (RFC 9457).

**Consolidado** — projeto único (`FluxoCaixa.Consolidado.Api`), sem estratificação em camadas: não
há domínio a proteger, a operação é uma soma agregada.

```
FluxoCaixa.Consolidado.Api
├── Consulta/          endpoint + DTO de resposta
├── Consumo/           consumidor do evento
├── Persistencia/      DbContext, entidades, migrações
└── Program.cs         composição, autenticação, limite de taxa
```

O contrato do evento (`EventoLancamentoRegistrado`) vive em `FluxoCaixa.Contratos`, um projeto
compartilhado sem dependência de ASP.NET nem de MassTransit, referenciado pelos dois serviços — é
o que garante que produtor e consumidor concordam sobre o mesmo tipo.

Cada serviço tem seu próprio projeto de teste: `FluxoCaixa.Lancamentos.Testes.Unidade` (domínio e
aplicação, sem I/O), `FluxoCaixa.Lancamentos.Testes.Integracao` e `FluxoCaixa.Consolidado.Testes`
(unitários e integração narrow via [Testcontainers](https://testcontainers.com/), um adaptador por
vez contra sua dependência real — PostgreSQL ou RabbitMQ —, mais os testes de borda da API contra
um `WebApplicationFactory`).

## Executando localmente

Pré-requisitos: Docker e Docker Compose.

```bash
docker compose up --build
```

Sobe os dois serviços, uma instância PostgreSQL por serviço (`fluxocaixa_lancamentos` e
`fluxocaixa_consolidado`, cada uma em seu próprio contêiner) e o RabbitMQ com um único comando, sem
preparação manual de ambiente. As migrações são aplicadas automaticamente na inicialização de cada
serviço. O Lançamentos fica disponível em `http://localhost:8080`, o Consolidado em
`http://localhost:8081`, e o painel de administração do RabbitMQ em `http://localhost:15672`
(usuário e senha: `fluxocaixa`).

Suba os dois juntos, nessa ordem ou com o mesmo comando: enquanto o Consolidado nunca tiver
subido ao menos uma vez, a fila que o alimenta ainda não existe, e o RabbitMQ descarta o que o
Lançamentos publica por falta de vínculo.

## Usando a API

### Lançamentos — registrar um lançamento

O serviço expõe uma única operação: registrar um lançamento.

```
POST /lancamentos
Authorization: Bearer <token>
Idempotency-Key: <chave opaca gerada pelo cliente, até 64 caracteres imprimíveis>
Content-Type: application/json

{
  "tipo": "credito",
  "valor": 150.00,
  "competencia": "2026-08-02",
  "descricao": "venda de balcão"
}
```

- `tipo`: `"credito"` ou `"debito"`.
- `valor`: estritamente positivo, no máximo duas casas decimais.
- `competencia`: data (`yyyy-MM-dd`), entre 90 dias atrás e a data corrente, ambos inclusive.
- `descricao`: obrigatória, até 200 caracteres, sem caracteres de controle.

O comerciante é determinado exclusivamente pela credencial (claim `sub` do token); qualquer
identificador de comerciante enviado no corpo é ignorado. A chave de idempotência é obrigatória: o
reenvio da mesma chave devolve a resposta original em vez de criar um segundo lançamento.

Resposta (`201 Created` na primeira vez, `200 OK` num reenvio):

```json
{
  "lancamentoId": "0195...-...",
  "recebidoEm": "2026-08-02T14:30:00Z",
  "criado": true
}
```

### Consolidado — consultar o saldo de uma data

O serviço expõe uma única operação de leitura: consultar o consolidado de uma data. Não existe
operação capaz de criar, alterar ou remover um saldo — o consolidado é dado derivado, e sua única
entrada é o fluxo de lançamentos consumido do Lançamentos.

```
GET /consolidado/{data}
Authorization: Bearer <token>
```

- `data`: `yyyy-MM-dd`. Formato inválido é rejeitado com `400`.

O comerciante é determinado exclusivamente pela credencial, do mesmo jeito que no Lançamentos.
Uma data sem nenhum lançamento retorna sucesso com os totais e o saldo zerados, não erro.

Resposta (`200 OK`):

```json
{
  "totalCredito": 300.00,
  "totalDebito": 120.00,
  "saldo": 180.00,
  "atualizadoEm": "2026-08-02T14:30:05Z"
}
```

`atualizadoEm` é o instante da última atualização daquele consolidado, e fica `null` quando a data
não teve movimentação — um instante inventado mentiria sobre a frescura da projeção exatamente no
caso em que o campo importa. É por esse campo que a defasagem entre o registro e o reflexo na
consulta fica observável para quem consome, em vez de escondida.

Erros dos dois serviços seguem o formato Problem Details, identificando a regra violada sem expor
detalhe interno:

```json
{
  "status": 422,
  "title": "ValorNaoPositivo",
  "type": "https://fluxocaixa.dev/erros/ValorNaoPositivo",
  "detail": "O valor deve ser estritamente positivo."
}
```

### Autenticação

O emissor de credenciais ainda não existe (é uma entrega futura). Enquanto isso, os dois serviços
validam localmente um JWT assinado com a chave configurada em `Autenticacao:ChaveDeAssinatura`
(`appsettings.json` ou variável de ambiente `Autenticacao__ChaveDeAssinatura`), sem consultar
nenhum emissor durante o atendimento da requisição. Para testar localmente, gere um token HS256
com emissor `fluxocaixa`, audiência `fluxocaixa` e uma claim `sub` com o identificador do
comerciante, assinado com a mesma chave configurada nos serviços — a mesma credencial é aceita
pelos dois. `scripts/gerar_token_teste.py` gera esse token.

## Executando os testes

```bash
dotnet test
```

Os testes de integração de ambos os serviços sobem contêineres reais de PostgreSQL e RabbitMQ via
Testcontainers e exigem Docker disponível no ambiente que roda os testes.

## Verificação de carga e defasagem

`scripts/carga_consolidado.py`, contra o ambiente do `docker-compose` já no ar, verifica a carga de
referência e mede a defasagem entre o registro e o reflexo no consolidado:

```bash
python3 scripts/carga_consolidado.py <comerciante-id> --rps 50 --duracao 30
python3 scripts/carga_consolidado.py <comerciante-id> --modo defasagem
```

Não roda em `dotnet test` nem bloqueia build — é um instrumento de verificação operacional manual,
não um teste de regressão.

## Limites e decisões conhecidas

- Instância única por serviço no ambiente local: cada serviço tem seu próprio contêiner PostgreSQL,
  isolando a falha de um do outro, mas réplicas, balanceamento e failover dentro de cada instância
  ficam para uma evolução futura; a meta de disponibilidade é objeto de desenho, não de demonstração
  empírica neste ambiente.
- Limite de taxa com estado em memória: com múltiplas réplicas, o limite efetivo se multiplica pelo
  número de réplicas.
- A política de limite de taxa e a validação de credencial estão duplicadas entre os dois serviços,
  em vez de extraídas para um projeto compartilhado — decisão deliberada enquanto só dois serviços
  existem; extrair agora seria adivinhar a forma certa da abstração.
- O emissor de credenciais, observabilidade, painel e documentação de arquitetura são entregas
  futuras, fora do escopo deste repositório nesta entrega.
- **Reconstrução do consolidado não implementada**: não há operação para reconstruir o consolidado
  a partir do histórico de lançamentos (RF-C13). O Lançamentos não expõe leitura e o Consolidado
  tem persistência independente, então reprocessar exigiria criar uma dessas duas superfícies.
  Perdido o banco do Consolidado, os saldos anteriores ao incidente não voltam. Fica como evolução,
  condicionada à perda total do banco do Consolidado ou a uma mudança na regra de agregação — até
  lá, o custo de construir a capacidade não se paga.
