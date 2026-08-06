# 004 — Retentativa com intervalos crescentes sem plugin do broker

## Contexto

Nem toda falha no consumo de um evento é igual. Uma falha transitória — banco momentaneamente fora
do ar, deadlock — tende a se resolver sozinha em segundos. Uma falha persistente — mensagem
irreconhecível, erro de programação — não se resolve por repetição, e insistir nela bloquearia o
processamento dos lançamentos seguintes.

O MassTransit resolve isso com dois mecanismos que compõem: `UseMessageRetry`, que tenta de novo
dentro do próprio processo, com um atraso entre tentativas; e `UseDelayedRedelivery`, que reentrega
a mensagem pelo próprio broker depois de um intervalo maior, sobrevivendo a um reinício do processo
entre tentativas. O `UseDelayedRedelivery` depende de um plugin específico do RabbitMQ — o de troca
(exchange) atrasada — que não está presente na imagem padrão usada no `docker-compose`.

## Decisão

Um único pipeline de `UseMessageRetry`, com intervalos crescentes, cobre as duas necessidades:

```csharp
endpoint.UseMessageRetry(retry => retry.Intervals(
    TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(30)));
```

O primeiro intervalo zero cobre a retentativa imediata; os intervalos maiores que seguem cobrem o
que o redelivery atrasado faria, sem exigir o plugin. Esgotadas as quatro tentativas, o MassTransit
move a mensagem automaticamente para a fila `fluxocaixa-consolidado_error` — a segregação por
dead-letter nativa do RabbitMQ, que foi um dos motivos declarados da escolha desse broker sobre o
Kafka.

Um consumidor próprio (`ConsumidorDeFalhaPersistente`) observa o `Fault<EventoLancamentoRegistrado>`
publicado quando a mensagem é segregada, e apenas incrementa a métrica de mensagens segregadas — não
intercepta nem reprocessa a mensagem.

## Alternativas consideradas

**Instalar o plugin de exchange atrasada.** Resolveria com a API nativa do MassTransit para
redelivery, mas adiciona uma dependência de infraestrutura à imagem do RabbitMQ por um ganho que o
intervalo crescente de `UseMessageRetry` já cobre no volume deste sistema. Descartada pelo custo de
composição do ambiente sem contrapartida funcional.

**Construir a segregação por falha persistente à mão** (contagem de tentativas própria, fila de erro
própria). Descartada: a dead-letter nativa do RabbitMQ já resolve isso, e reconstruí-la desperdiçaria
o motivo que levou à escolha desse broker.
