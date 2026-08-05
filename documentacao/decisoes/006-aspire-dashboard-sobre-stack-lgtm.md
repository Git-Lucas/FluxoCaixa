# 006 — Aspire Dashboard em vez da stack LGTM

## Contexto

Os três serviços exportam telemetria via OpenTelemetry (OTLP): rastros, métricas e logs. Alguma
ferramenta precisa recebê-la e torná-la explorável, dentro de uma restrição declarada — o custo de
composição do ambiente local precisa continuar compatível com um prazo curto, o que significa: um
contêiner a mais, sem volume adicional, sem arquivo de configuração próprio.

## Decisão

`mcr.microsoft.com/dotnet/aspire-dashboard`, recebendo OTLP na porta 4317 e servindo a interface na
18888, com acesso anônimo habilitado por variável de ambiente.

## Alternativa considerada: `grafana/otel-lgtm`

Consolida Collector, Prometheus, Tempo, Loki e Grafana num único contêiner, o que também atenderia à
restrição de "um contêiner a mais". Descartada pelo custo: cerca de 1,2 GB de imagem contra 250 MB
do Aspire Dashboard, cinco processos internos contra um, e provisionamento de datasources e
dashboards via arquivo YAML contra uma única variável de ambiente.

**O código da aplicação é idêntico nos dois casos** — os dois recebem OTLP sem exigir SDK ou
biblioteca diferente —, então a escolha é inteiramente de infraestrutura, e o critério decisivo é o
custo de composição do ambiente, que é uma restrição documentada do projeto.

## O que se abre mão

O Aspire Dashboard não tem persistência entre reinícios, linguagem de consulta sobre as métricas,
alertas nem retenção de longo prazo. Essa lista coincide com o que já está fora do escopo do
projeto: retenção de longo prazo, agregação histórica, análise de tendência, alertas e resposta a
incidentes não fazem parte da entrega. A ferramenta é declaradamente de desenvolvimento e diagnóstico
de curto prazo — que é exatamente o ambiente que este sistema tem.

**Consequência assumida:** a telemetria vive numa janela em memória e se perde a cada reinício do
contêiner do dashboard. É o gatilho declarado para a evolução — ver
["Melhorias futuras"](../../README.md#stack-de-observabilidade-com-retenção) no README.
