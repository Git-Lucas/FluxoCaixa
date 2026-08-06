# 008 — Fronteira entre métrica e log pela cardinalidade

## Contexto

O sistema precisa responder duas perguntas de natureza diferente sobre o mesmo evento — por exemplo,
uma mensagem segregada por falha persistente de processamento: "quantas segregações aconteceram" e
"de qual comerciante". A resposta óbvia — colocar o identificador do comerciante como rótulo
(dimensão) da métrica — é o antipadrão clássico de cardinalidade em sistemas de observabilidade:
rótulo de métrica com valor não limitado multiplica o número de séries armazenadas por esse valor, e
um comerciante é, por definição, não limitado.

## Decisão

**Nenhuma métrica leva o identificador do comerciante como dimensão.** O contador de mensagens
segregadas (`MetricasDoConsolidado.MensagensSegregadas`), por exemplo, não tem dimensão alguma além
do que o próprio OpenTelemetry já anexa como atributo de recurso (`service.name`) — a mesma série
existe com três comerciantes ou com três milhões.

O recorte por comerciante vem do **log estruturado**, onde o comerciante é atributo de cada evento —
anexado como escopo de log logo depois da autenticação (`UsarIdentificacaoDoComercianteNoLog`).
Contar eventos por comerciante é uma consulta sobre esse log, não uma dimensão de métrica.

```
   evento (ex.: mensagem segregada por falha persistente)
        │
        ├──▶ métrica: incrementa contador sem dimensão de comerciante   → "quanto"
        └──▶ log: linha com o comerciante como atributo do evento       → "de quem"
```

## A regra, generalizada

A fronteira entre métrica e log é a cardinalidade do recorte que se precisa fazer. Uma pergunta
"quanto" sobre uma dimensão de cardinalidade baixa e conhecida (por serviço, por tipo de erro) cabe
em métrica. Uma pergunta "de quem" sobre uma dimensão de cardinalidade alta e não controlada (por
comerciante, por usuário) cabe em log — nunca em métrica.

O mesmo princípio governa o log estruturado de toda requisição: o comerciante autenticado é anexado
como escopo de log logo depois da autenticação, nunca como rótulo de métrica.

**Alternativa considerada: usar o identificador do comerciante como rótulo de métrica diretamente.**
Descartada porque é o antipadrão documentado de cardinalidade — a saída em escala real exigiria
infraestrutura de particionamento por inquilino (dimensionamento à parte, como fazem Thanos, Cortex
ou Mimir), incompatível com o prazo e com o ambiente deste projeto.
