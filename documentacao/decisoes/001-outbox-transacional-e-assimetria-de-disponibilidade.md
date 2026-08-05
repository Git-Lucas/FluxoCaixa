# 001 — Outbox transacional e a assimetria "registrar é crítico, consultar é degradável"

## Contexto

O Lançamentos é a única fonte da verdade financeira do sistema: um lançamento não gravado é perda
irrecuperável, porque não existe outro lugar de onde derivá-lo. O Consolidado, do outro lado, expõe
um saldo derivado — defasado por alguns segundos é aceitável, indisponível por um tempo é aceitável.

Essa assimetria — **registrar é crítico, consultar é degradável** — governa o desenho inteiro do
Lançamentos, e em particular a forma como ele publica o evento que o Consolidado consome.

## Decisão

O lançamento, o registro de idempotência e uma linha de outbox são gravados **na mesma transação**.
Um publicador em segundo plano (`BusOutboxDeliveryService`, do MassTransit) varre as linhas
pendentes e as despacha para o RabbitMQ:

```
┌──────── uma transação ────────┐
│  lancamento                   │
│  outbox (despachado_em NULL)  │      ← confirma ao comerciante aqui
│  requisicao_idempotente       │
└───────────────────────────────┘
              ┆
   [publicador em segundo plano]
     SELECT ... WHERE despachado_em IS NULL
     ORDER BY id FOR UPDATE SKIP LOCKED LIMIT n
              ┆
        publica ──▶ marca despachado_em
```

A confirmação ao comerciante acontece no commit da transação, antes de qualquer tentativa de
publicação. O transporte de mensagens nunca está no caminho crítico do registro: se o RabbitMQ
estiver fora do ar, o lançamento é confirmado do mesmo jeito, e a linha de outbox fica pendente até
o transporte voltar.

**A ordem publica-depois-marca é deliberada.** Uma falha entre publicar e marcar como despachado
republica a mensagem — entrega repetida é absorvida pela deduplicação do Consolidado (ver
[003](003-deduplicacao-manual-no-consolidado.md)). A ordem inversa perderia a mensagem, o que é
inaceitável dado que a meta de perda é zero.

## Alternativas consideradas

**Publicação direta na requisição.** Publicar o evento antes de responder ao comerciante acopla a
latência do registro à disponibilidade do transporte, e uma falha de publicação depois do commit do
lançamento perde a mensagem sem chance de recuperação. Descartada por violar as duas metas que a
assimetria protege: confirmação sem depender do transporte, e perda zero.

**Transação distribuída entre banco e broker.** Resolveria o mesmo problema sem outbox, mas com
complexidade desproporcional ao prazo do projeto e suporte ruim no ecossistema .NET/RabbitMQ.
Descartada.

## Consequência estrutural: perda zero

Como o lançamento e a linha de outbox commitam na mesma transação, não existe estado em que um
exista sem o outro. Essa é a base da invariante de perda zero, tratada como propriedade estrutural
e não como meta numérica — ver [metas operacionais](../metas-operacionais.md).
