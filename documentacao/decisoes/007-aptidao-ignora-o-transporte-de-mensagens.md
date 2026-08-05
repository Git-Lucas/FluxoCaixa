# 007 — Aptidão que ignora o transporte de mensagens

## Contexto

Cada serviço expõe duas verificações de saúde: `/health/healthy`, que responde enquanto o processo
está de pé, e `/health/ready`, que reflete se o serviço está apto a atender requisições. A prática comum
é a aptidão verificar todas as dependências configuradas — banco, broker, serviços externos. Este
sistema se desvia dela deliberadamente.

## Decisão

A aptidão de cada serviço considera **apenas** o que aquele serviço precisa para atender as
requisições que expõe:

| Serviço | Postgres | RabbitMQ | Motivo |
|---|---|---|---|
| Lançamentos | sim | **não** | o outbox transacional existe para que o registro não dependa do transporte |
| Consolidado | sim | **não** | sem broker ele serve saldo defasado, que é o contrato declarado |
| Identidade | — | — | aptidão é ter o par de chaves de assinatura carregado; não há outra dependência |

## Por que o RabbitMQ fica de fora

Incluir o RabbitMQ na aptidão do Lançamentos faria um broker fora do ar tirar de rotação o serviço
crítico do sistema, por causa de uma dependência que ele deliberadamente removeu do caminho da
requisição. A meta de disponibilidade de 99,9% cairia por causa de uma verificação de saúde — e o
outbox transacional, que é a decisão central do sistema (ver
[001](001-outbox-transacional-e-assimetria-de-disponibilidade.md)), seria desfeito pela porta dos
fundos.

A mesma lógica vale para o Consolidado: o transporte parado degrada a frescura do saldo, que é
precisamente o que a consistência eventual admite — não a capacidade do serviço de responder a uma
consulta.

**Alternativa considerada: aptidão verificando todas as dependências configuradas** — o padrão que a
maioria dos exemplos de health check mostra. Descartada por contradizer o próprio desenho do sistema:
a aptidão precisa refletir a assimetria "registrar é crítico, consultar é degradável", não a lista de
tudo que o serviço eventualmente usa.

Os endpoints (`/health/healthy` e `/health/ready`) não exigem credencial e não expõem detalhe interno
na resposta — nem string de conexão, nem rastro de exceção.
