# 003 — Deduplicação escrita à mão no Consolidado

## Contexto

O RabbitMQ, como a maioria dos transportes de mensagem, entrega ao-menos-uma-vez: uma mensagem pode
chegar duas vezes, seja por retentativa do produtor, seja porque a confirmação (`ack`) se perdeu
entre o commit da transação de consumo e a resposta ao broker. O Consolidado precisa refletir cada
lançamento **exatamente uma vez**, independentemente de quantas vezes o evento chega.

O MassTransit oferece um mecanismo pronto para isso — o **inbox transacional**, que grava cada
mensagem recebida numa tabela própria antes de processá-la, e usa essa tabela para descartar
repetições.

## Decisão

A deduplicação é feita à mão, numa tabela própria (`lancamento_processado`), e a mesma transação que
grava a marca de processado grava também o incremento no agregado:

```
    mensagem chega
          │
          ▼
   ┌──── uma transação ─────────────────────────────┐
   │  INSERT lancamento_processado (lancamento_id)  │
   │      ON CONFLICT DO NOTHING                    │
   │      0 linhas afetadas ──▶ duplicata, sai      │
   │                                                │
   │  INSERT consolidado_diario ... ON CONFLICT     │
   │      DO UPDATE SET total = total + excluded    │
   └────────────────────┬───────────────────────────┘
                        │ commit
                        ▼
                       ack
```

A duplicata é detectada por `0 linhas afetadas` do `ON CONFLICT DO NOTHING`, não por captura de
exceção de violação de unicidade: no PostgreSQL essa violação aborta a transação inteira, e o
incremento do agregado — que precisa acontecer na mesma transação — falharia junto. Recuperar
exigiria um `SAVEPOINT`; evitar o erro sai mais barato que tratá-lo.

O incremento do agregado é `INSERT ... ON CONFLICT DO UPDATE SET total = total + excluded.total`,
executado como SQL direto, não como leitura-modificação-escrita via EF Core. Carregar a linha, somar
em memória e salvar perde atualizações quando dois consumidores processam lançamentos do mesmo dia
em paralelo: ambos leem o mesmo total antes de qualquer um gravar. O `ON CONFLICT DO UPDATE` é
atômico no PostgreSQL e serializa no lock da própria linha.

**A confirmação (`ack`) só acontece depois do commit.** O MassTransit confirma a mensagem apenas
quando o método de consumo termina sem lançar exceção — a ordem inversa arriscaria perder o
lançamento se o processo morresse entre o `ack` e o commit. Na dúvida, processar duas vezes é
preferível a perder, e é exatamente essa dúvida que a tabela de deduplicação absorve.

## Por que não o inbox do MassTransit

O inbox resolveria a deduplicação, mas deixaria o incremento do agregado como uma segunda escrita
fora do controle da mesma transação atômica que o `ON CONFLICT DO UPDATE` exige — o mecanismo do
MassTransit garante que a mensagem não é reprocessada, não que um incremento em SQL bruto e a marca
de deduplicação aconteçam com a mesma semântica de conflito na mesma instrução. Construir a
deduplicação como parte da própria transação de negócio mantém as duas escritas — marca e
incremento — atômicas uma em relação à outra, sem depender de um mecanismo genérico do framework
para uma garantia que aqui precisa ser específica do domínio.

**Alternativa considerada: limitar o consumidor a uma mensagem por vez**, o que evitaria a corrida
de concorrência sem precisar de `ON CONFLICT DO UPDATE` atômico. Descartada por estrangular a vazão
do consumo e comprometer a meta de defasagem.

## Lock consultivo antes do INSERT de deduplicação

O consumidor toma um lock consultivo de transação (`pg_advisory_xact_lock`, liberado automaticamente
no commit/rollback) sobre o identificador do lançamento antes do `INSERT ... ON CONFLICT DO NOTHING`
em `lancamento_processado`. Sem ele, duas entregas concorrentes do mesmo evento poderiam passar pelo
`ON CONFLICT DO NOTHING` sem esperar uma pela outra — o PostgreSQL não serializa esse `INSERT` contra
uma transação concorrente ainda não commitada. Se a primeira entrega revertesse depois de inserir a
linha de dedup mas antes do commit, a segunda já teria visto "0 linhas inseridas", pulado a agregação
e confirmado a mensagem: o lançamento seria perdido. O lock força a segunda entrega a esperar a
primeira terminar (commit ou rollback) antes de decidir se há de fato conflito.

## Consequência: soma comutativa, ordem irrelevante

Como o incremento é uma soma, a ordem de chegada das mensagens não importa — não há necessidade de
que o transporte preserve ordem, o que também dispensa fila particionada por comerciante no volume
atual (ver ["Melhorias futuras"](../../README.md#escada-de-gatilhos) no README para o gatilho de
quando isso deixa de valer).
