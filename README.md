# FluxoCaixa — Serviço de Lançamentos

Serviço que registra os lançamentos de crédito e débito do caixa de um comerciante. É a fonte da
verdade financeira do sistema: o histórico é imutável, e nenhuma outra operação além do registro é
exposta (não há leitura, alteração, exclusão nem estorno).

O desenho é governado por uma assimetria deliberada: **registrar é crítico, consultar é
degradável**. O serviço confirma o registro sem depender do transporte de mensagens (RabbitMQ) nem
de qualquer consumidor a jusante — a propagação do evento acontece fora do caminho da requisição,
via padrão outbox transacional.

## Arquitetura

Clean Architecture / Hexagonal, com quatro projetos na raiz da solução, dependência sempre apontando
para dentro:

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

Dois projetos de teste: `FluxoCaixa.Lancamentos.Testes.Unidade` (domínio e aplicação, sem I/O — a
maioria dos testes) e `FluxoCaixa.Lancamentos.Testes.Integracao` (integração narrow via
[Testcontainers](https://testcontainers.com/), um adaptador por vez contra sua dependência real —
PostgreSQL ou RabbitMQ —, mais os testes de borda da API contra um `WebApplicationFactory`).

## Executando localmente

Pré-requisitos: Docker e Docker Compose.

```bash
docker compose up --build
```

Sobe o serviço, o PostgreSQL e o RabbitMQ com um único comando, sem preparação manual de ambiente.
A migração do banco é aplicada automaticamente na inicialização do serviço. A API fica disponível
em `http://localhost:8080`; o painel de administração do RabbitMQ, em `http://localhost:15672`
(usuário e senha: `fluxocaixa`).

## Usando a API

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

Erros seguem o formato Problem Details, identificando a regra violada sem expor detalhe interno:

```json
{
  "status": 422,
  "title": "ValorNaoPositivo",
  "type": "https://fluxocaixa.dev/erros/ValorNaoPositivo",
  "detail": "O valor deve ser estritamente positivo."
}
```

### Autenticação

O emissor de credenciais ainda não existe (é uma entrega futura). Enquanto isso, o serviço valida
localmente um JWT assinado com a chave configurada em `Autenticacao:ChaveDeAssinatura`
(`appsettings.json` ou variável de ambiente `Autenticacao__ChaveDeAssinatura`), sem consultar
nenhum emissor durante o atendimento da requisição. Para testar localmente, gere um token HS256
com emissor `fluxocaixa`, audiência `fluxocaixa-lancamentos` e uma claim `sub` com o identificador
do comerciante, assinado com a mesma chave configurada no serviço.

## Executando os testes

```bash
dotnet test
```

Os testes de `FluxoCaixa.Lancamentos.Testes.Integracao` sobem contêineres reais de PostgreSQL e
RabbitMQ via Testcontainers e exigem Docker disponível no ambiente que roda os testes.

## Limites e decisões conhecidas

- Instância única no ambiente local: réplicas, balanceamento e failover ficam para uma evolução
  futura; a meta de disponibilidade é objeto de desenho, não de demonstração empírica neste
  ambiente.
- Limite de taxa com estado em memória: com múltiplas réplicas, o limite efetivo se multiplica pelo
  número de réplicas.
- O consolidado de saldo, o emissor de credenciais, observabilidade e painel são serviços
  separados, fora do escopo deste repositório nesta entrega.
- Este serviço só publica, nunca consome: não declara fila para o evento de lançamento. A fila real
  nasce quando o serviço de consolidado (change futura) declarar seu próprio consumidor vinculado ao
  mesmo tipo de evento. Eventos publicados antes disso existir não ficam retidos em nenhuma fila — o
  consolidado se reconstrói relendo a tabela de lançamentos, a fonte da verdade, não o histórico de
  mensagens.
