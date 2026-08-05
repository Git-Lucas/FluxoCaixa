# FluxoCaixa — Lançamentos, Consolidado e Identidade

Três serviços independentes. **Lançamentos** registra créditos e débitos do caixa de um
comerciante: é a fonte da verdade financeira do sistema, o histórico é imutável, e nenhuma outra
operação além do registro é exposta (não há leitura, alteração, exclusão nem estorno).
**Consolidado** consome o evento publicado pelo Lançamentos e expõe o saldo diário resultante —
total de créditos, total de débitos e saldo líquido de uma data. **Identidade** é o emissor de
credenciais: autentica o comerciante por `client_credentials` e assina a credencial que os outros
dois validam.

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

**Identidade** — projeto único (`FluxoCaixa.Identidade.Api`), sem banco: o emissor de credenciais,
com o par de chaves RSA gerado na primeira inicialização e persistido em volume nomeado.

```
FluxoCaixa.Identidade.Api
├── Chave/             carga/geração da chave RSA, derivação do kid
├── Emissao/           POST /connect/token (client_credentials)
├── Descoberta/        GET /.well-known/{jwks.json,openid-configuration}
└── Program.cs         composição, limite de taxa
```

O contrato do evento (`EventoLancamentoRegistrado`) vive em `FluxoCaixa.Contratos`, um projeto
compartilhado sem dependência de ASP.NET nem de MassTransit, referenciado pelos dois serviços de
negócio — é o que garante que produtor e consumidor concordam sobre o mesmo tipo. A política de
limite de taxa vive em `FluxoCaixa.Plataforma`, compartilhada pelos três serviços.

Cada serviço tem seu próprio projeto de teste: `FluxoCaixa.Lancamentos.Testes.Unidade` (domínio e
aplicação, sem I/O), `FluxoCaixa.Lancamentos.Testes.Integracao`, `FluxoCaixa.Consolidado.Testes` e
`FluxoCaixa.Identidade.Testes` (unitários e integração narrow via
[Testcontainers](https://testcontainers.com/) quando há dependência real — PostgreSQL ou RabbitMQ
—, mais os testes de borda da API contra um `WebApplicationFactory`; o Identidade não tem banco nem
transporte de mensagens, então seus testes rodam sem Docker).

## Executando localmente

Pré-requisitos: Docker e Docker Compose.

```bash
docker compose up --build
```

Sobe os três serviços, uma instância PostgreSQL por serviço de negócio (`fluxocaixa_lancamentos` e
`fluxocaixa_consolidado`, cada uma em seu próprio contêiner) e o RabbitMQ com um único comando, sem
preparação manual de ambiente. As migrações são aplicadas automaticamente na inicialização de cada
serviço de negócio, e a chave de assinatura do Identidade é gerada na sua primeira inicialização.
O Lançamentos fica disponível em `http://localhost:8080`, o Consolidado em `http://localhost:8081`,
o Identidade em `http://localhost:8082`, e o painel de administração do RabbitMQ em
`http://localhost:15672` (usuário e senha: `fluxocaixa`).

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

A credencial é obtida do Identidade — um comerciante é um cliente `client_credentials` (RFC 6749
§4.4), autenticado por `client_secret_basic`:

```
POST /connect/token
Authorization: Basic <base64(client_id:client_secret)>
Content-Type: application/x-www-form-urlencoded

grant_type=client_credentials
```

Resposta (`200 OK`):

```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsI...",
  "token_type": "Bearer",
  "expires_in": 900
}
```

O `client_id` é o identificador do comerciante (claim `sub` da credencial emitida). O
`docker-compose.yml` semeia dois clientes de desenvolvimento, `comerciante-1` e `comerciante-2`,
com o segredo declarado ali mesmo — troque-os antes de qualquer uso além do ambiente local. Cliente
inexistente e segredo incorreto recebem a mesma resposta (`401`, `{"error": "invalid_client"}`),
para não revelar quais comerciantes existem. A credencial dura 15 minutos; não há renovação nem
revogação — expirada, o cliente autentica de novo.

Os dois serviços de negócio validam a credencial localmente (`Autenticacao:Authority` apontando
para o Identidade), sem consultar o emissor durante o atendimento da requisição: a chave pública é
obtida e mantida em cache pelo próprio `JwtBearer`, via
`GET /.well-known/openid-configuration` e `GET /.well-known/jwks.json`. Nenhum dos dois guarda
material capaz de assinar uma credencial, só de verificar.

`Autenticacao:RequererHttps` fica desligado apenas no `docker-compose` (o Identidade não tem TLS
dentro da rede do compose) — ligá-lo é obrigatório fora desse ambiente.

### Depurando um serviço fora do container

Para rodar um dos serviços de negócio direto do `dotnet run`/IDE (com o restante subindo via
`docker compose`), pare o container equivalente antes — ex.: `docker compose stop lancamentos` —
para liberar a porta. O `launchSettings.json` de cada projeto já define
`ASPNETCORE_ENVIRONMENT=Development`, e o `appsettings.Development.json` correspondente desliga
`Autenticacao:RequererHttps` pelo mesmo motivo do compose (o Identidade exposto em
`localhost:8082` também não tem TLS fora da rede do container).

## Executando os testes

```bash
dotnet test
```

Os testes de integração do Lançamentos e do Consolidado sobem contêineres reais de PostgreSQL e
RabbitMQ via Testcontainers e exigem Docker disponível no ambiente que roda os testes. O Identidade
não tem banco nem transporte de mensagens, então seus testes rodam sem Docker.

## Verificação de carga e defasagem

`scripts/carga_consolidado.py`, contra o ambiente do `docker-compose` já no ar, verifica a carga de
referência e mede a defasagem entre o registro e o reflexo no consolidado:

```bash
python3 scripts/carga_consolidado.py comerciante-1 <segredo-de-comerciante-1> --rps 50 --duracao 30
python3 scripts/carga_consolidado.py comerciante-1 <segredo-de-comerciante-1> --modo defasagem
```

A credencial é obtida do Identidade (`http://localhost:8082` por padrão) e reaproveitada durante
toda a corrida, para que o limite de 10 req/s da emissão nunca interfira na medição.

Não roda em `dotnet test` nem bloqueia build — é um instrumento de verificação operacional manual,
não um teste de regressão.

## Verificação de CA-I01 e CA-I02

Os dois critérios atravessam os três serviços e não viram teste automatizado (design.md, decisão
12) — o roteiro abaixo reproduz a verificação manualmente, com o compose já no ar:

```bash
# 1. Obter uma credencial do emissor
TOKEN=$(python3 - <<'PY'
import base64, json, urllib.request
credenciais = base64.b64encode(b"comerciante-1:segredo-comerciante-1-troque-em-producao").decode()
req = urllib.request.Request(
    "http://localhost:8082/connect/token",
    data=b"grant_type=client_credentials",
    method="POST",
    headers={"Authorization": f"Basic {credenciais}", "Content-Type": "application/x-www-form-urlencoded"},
)
print(json.loads(urllib.request.urlopen(req).read())["access_token"])
PY
)

# 2. Uma requisição bem-sucedida a cada serviço de negócio, para aquecer o cache de cada um
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:8081/consolidado/$(date +%F) -H "Authorization: Bearer $TOKEN"
curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:8080/lancamentos \
  -H "Authorization: Bearer $TOKEN" -H "Idempotency-Key: ca-i02-$(date +%s)" -H "Content-Type: application/json" \
  -d '{"tipo":"credito","valor":1.00,"competencia":"'"$(date +%F)"'","descricao":"verificacao CA-I02"}'

# 3. Parar o emissor (stop, não down: down remove a rede e a falha vira timeout de DNS)
docker compose stop identidade

# 4. Repetir as duas requisições do passo 2 — ambas devem continuar respondendo com sucesso
```

Prova o cenário do critério — emissor parado, credencial já emitida continua aceita pelos dois
serviços — e não mais que isso: não cobre rotação de chave disparada e falhando (design.md, decisão
12). Descarte a credencial e reinicie o Identidade (`docker compose start identidade`) ao final.

## Limites e decisões conhecidas

- Instância única por serviço no ambiente local: cada serviço tem seu próprio contêiner PostgreSQL,
  isolando a falha de um do outro, mas réplicas, balanceamento e failover dentro de cada instância
  ficam para uma evolução futura; a meta de disponibilidade é objeto de desenho, não de demonstração
  empírica neste ambiente.
- Limite de taxa com estado em memória, por instância: com múltiplas réplicas de qualquer um dos
  três serviços, o limite efetivo se multiplica pelo número de réplicas.
- Observabilidade, painel e documentação de arquitetura são entregas futuras, fora do escopo deste
  repositório nesta entrega.
- **Janela fria de inicialização**: um serviço de negócio que inicia sem a chave de verificação em
  cache e com o Identidade indisponível rejeita credencial até conseguir obtê-la; volta a aceitar
  sozinho, sem intervenção manual, assim que o Identidade estiver acessível.
- **`docker compose down -v` invalida as credenciais em circulação**: o volume da chave do
  Identidade é apagado junto com os bancos. Chave nova, `kid` novo, credenciais emitidas antes
  passam a ser rejeitadas.
- **Sem revogação, introspecção ou renovação**: a validade de 15 minutos é o único mecanismo que
  encerra uma credencial — vazou, vale até expirar.
- **Réplicas frias simultâneas do Identidade gerariam chaves divergentes**: duas instâncias subindo
  ao mesmo tempo com o volume vazio cada uma geraria a sua própria chave. Não se aplica ao ambiente
  local, de instância única.
- **Metadados do Identidade obtidos sem TLS no ambiente local**: `Autenticacao:RequererHttps` fica
  desligado apenas dentro da rede do `docker-compose` — inaceitável fora dela.
- **Segredos de cliente em texto claro no repositório**: a semente de `docker-compose.yml` é
  declaradamente de desenvolvimento; em ambiente real viriam de cofre gerenciado.
- **Reconstrução do consolidado não implementada**: não há operação para reconstruir o consolidado
  a partir do histórico de lançamentos (RF-C13). O Lançamentos não expõe leitura e o Consolidado
  tem persistência independente, então reprocessar exigiria criar uma dessas duas superfícies.
  Perdido o banco do Consolidado, os saldos anteriores ao incidente não voltam. Fica como evolução,
  condicionada à perda total do banco do Consolidado ou a uma mudança na regra de agregação — até
  lá, o custo de construir a capacidade não se paga.
