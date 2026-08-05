# Metas operacionais

## Metas e suas medidas

| Meta | Número | Medida |
|---|---|---|
| Disponibilidade — Lançamentos | 99,9% | Razão sobre a métrica nativa de duração de requisição (`http.server.request.duration`), segregada por rota e por código de status — ver [fórmula](#fórmula-de-disponibilidade) |
| Disponibilidade — Consolidado | 95% | Mesma métrica de origem, recortada pela rota do Consolidado |
| Latência p99 — registro de lançamento | até 200 ms | Distribuição da mesma métrica de duração, recortada pela rota `POST /lancamentos` |
| Latência p95 — consulta de consolidado | até 100 ms | Distribuição da mesma métrica de duração, recortada pela rota `GET /consolidado/{data}` |
| Defasagem de consolidação p95 | até 5 s | Distribuição medida no consumo: diferença entre o instante de registro do lançamento e o instante em que o consumidor grava o reflexo no agregado |
| Mensagens segregadas por falha persistente | até 0,1% | Contagem de mensagens segregadas (`ConsumidorDeFalhaPersistente`) sobre o total de mensagens consumidas |

Cada meta acima tem métrica correspondente exportada via OTLP, explorável pelo Aspire Dashboard (ver
[decisão 006](decisoes/006-aspire-dashboard-sobre-stack-lgtm.md)).

## Fórmula de disponibilidade

```
disponibilidade = 1 − (requisições com status 5xx) / total de requisições
```

Uma resposta 4xx de validação de negócio conta como atendida: o serviço respondeu corretamente a
uma requisição inválida, o que é disponibilidade, não falha.

## Perda zero: invariante estrutural, fora da tabela de metas

"Perda de lançamentos publicados: zero" não é uma meta medida por número. É uma **propriedade
garantida pelo desenho**: o lançamento e a linha de outbox commitam na mesma transação (ver
[decisão 001](decisoes/001-outbox-transacional-e-assimetria-de-disponibilidade.md)), e não existe
estado em que um exista sem o outro.

Colocar essa linha na tabela de metas, com uma medida em branco ou aproximada, misturaria ambição com
invariante — e qualquer medida aproximada estaria na prática medindo defasagem de consolidação com o
nome de perda, não perda de fato.

O observável que acompanha essa invariante é o **volume pendente de publicação** — a contagem de
linhas de outbox ainda não despachadas, lida periodicamente da tabela pelo `MedidorDeVolumePendente`.
Com o transporte de mensagens parado, esse volume cresce de forma visível; restabelecido o transporte,
ele retorna a zero sem intervenção manual. É esse comportamento — crescer sob falha e zerar depois —
que verifica a invariante, não um número de perda que nunca deveria deixar de ser zero.

## Disponibilidade: desenho e medição, não demonstração empírica local

As metas de disponibilidade (99,9% e 95%) são objeto de **desenho e medição**, não de demonstração
empírica no ambiente local. O `docker-compose` sobe uma única instância de cada serviço, de cada
banco e do RabbitMQ — não há réplica, balanceamento nem failover para exercitar, e uma instância única
não pode, por construção, comprovar uma disponibilidade de 99,9% ao longo do tempo: um único evento
de indisponibilidade nesse ambiente já teria peso desproporcional em qualquer janela de medição curta.

O que este sistema garante é o desenho que torna essas metas alcançáveis — a assimetria entre
registro crítico e consulta degradável, o outbox transacional, a aptidão que não confunde
indisponibilidade do transporte com indisponibilidade do serviço — e a instrumentação que mede a
métrica de origem corretamente, pronta para ser avaliada contra a meta assim que o sistema rodar num
ambiente com mais de uma instância por serviço.
