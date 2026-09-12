# 009 — Postura de segurança frente ao OWASP Top 10:2025

## Contexto

As decisões de segurança do sistema estão espalhadas pelos registros anteriores — a assimetria de
disponibilidade em [001](001-outbox-transacional-e-assimetria-de-disponibilidade.md), o emissor sem
banco e sem revogação em [005](005-emissor-sem-banco-e-ausencia-de-revogacao.md), a fronteira entre
métrica e log em [008](008-fronteira-entre-metrica-e-log-por-cardinalidade.md) — e as ausências
declaradas estão no `README` da raiz. Nenhum documento, porém, respondia à pergunta que um revisor
faz primeiro: **quais controles existem por decisão e quais faltam por decisão.**

Este registro fixa essa leitura, tomando como critério externo o OWASP Top 10:2025 e o Authentication
Cheat Sheet (referências ao final). A revisão é de **12 de setembro de 2026**, contra a edição 2025
do Top 10 — duas categorias dela são novas em relação à edição anterior (A03, cadeia de suprimentos,
e A10, condições excepcionais), e é justamente onde um sistema desenhado sob a edição de 2021 tende a
não ter resposta pronta.

## Decisão: o Top 10 é critério de revisão, não checklist de conformidade

O sistema é um desafio técnico de poucos dias, não um produto em produção. Perseguir os dez capítulos
até o fim produziria camadas de defesa sem ameaça correspondente — e a ausência de um controle deixa
de ser um risco quando é deliberada, registrada e tem gatilho.

Por isso a avaliação classifica cada recomendação em três faixas, e só a primeira é tratada como
dívida:

1. **Atendido** — há controle em código, com evidência no arquivo indicado.
2. **Lacuna com gatilho** — não implementado, com o evento ou a escala que passaria a justificá-lo
   registrado na tabela de evoluções.
3. **Não aplicável** — a recomendação pressupõe uma superfície que este sistema não tem (sessão,
   senha de usuário humano, upload de arquivo, renderização de HTML).

A terceira faixa importa tanto quanto as outras: sem ela, a ausência de MFA ou de invalidação de
sessão é lida como esquecimento, e não como consequência de autenticação máquina-a-máquina sem
estado.

## O que está atendido

### A01 — Controle de acesso quebrado

O comerciante da requisição vem **exclusivamente** da claim do token, nunca da rota ou do corpo
(`EndpointsDeLancamentos`, `EndpointsDeConsulta`): não existe identificador de tenant manipulável na
superfície pública, o que elimina a classe de referência direta insegura em vez de defendê-la caso a
caso. O isolamento é reforçado uma camada abaixo, por filtro global de consulta no EF Core
(`LancamentosDbContext`, `ConsolidadoDbContext`) — o controle é escrito uma vez e vale para qualquer
consulta escrita depois. `ContextoComerciante` falha fechado nos dois sentidos: lança se o tenant não
foi definido e lança se alguém tentar redefini-lo no meio da requisição. Data sem consolidado devolve
zeros, não `404`, para não permitir inferir o que existe.

### A02 — Configuração insegura

Toda configuração sensível é validada **na subida**, não no primeiro uso: cliente do emissor,
`Authority` da autenticação, string de conexão e endereço do broker derrubam o processo se ausentes
ou inválidos. O tratamento de erro é centralizado (`AddProblemDetails` + `UseExceptionHandler`) e
nenhuma resposta carrega stack trace. A imagem final é a de runtime, não a do SDK.

### A03 — Falhas na cadeia de suprimentos

`Directory.Packages.props` fixa **todas** as versões, com `CentralPackageTransitivePinningEnabled`
estendendo o pino às dependências transitivas — que é exatamente o "rastreamento inadequado de
dependências transitivas" que a categoria aponta. O arquivo também registra o porquê de dois pinos
específicos: `Microsoft.OpenApi` fixado acima da versão vulnerável trazida transitivamente, e
MassTransit fixado na última versão sob licença aberta. Todos os componentes vêm de fonte oficial.

### A04 — Falhas criptográficas

RS256 com RSA de 2048 bits, e assimetria real de confiança: os serviços de negócio apenas verificam,
nunca guardam material capaz de assinar. O `kid` é derivado por SHA-256 do JWK canônico
(`ChaveDeAssinatura`), de modo que trocar a chave invalida naturalmente o que foi emitido antes. A
comparação do segredo do cliente usa `CryptographicOperations.FixedTimeEquals` **com normalização de
comprimento** (`EndpointsDeToken`), para que nem o tamanho do segredo configurado vaze pelo tempo de
resposta. Nenhum algoritmo obsoleto no código: SHA-256 aparece só onde cabe (integridade de conteúdo
idempotente), nunca como armazenamento de credencial.

### A05 — Injeção

O caminho de negócio é todo parametrizado pelo EF Core. Os três comandos SQL escritos à mão no
consumidor do Consolidado são literais fixos com interpolação parametrizada — nenhum fragmento do
comando vem de dado de entrada. Como o lançamento é sempre crédito **ou** débito, o `UPSERT` soma as
duas colunas de total (uma delas com zero) em vez de escolher o nome da coluna em tempo de execução;
o tipo continua validado por lista de permissão em `TotaisDoLancamento`, que lança em qualquer valor
fora do conjunto conhecido — validação positiva, que é a recomendação da categoria, e coberta por
teste. A entrada é validada nos objetos de valor do domínio (`Dinheiro`, `Descricao`,
`DataCompetencia`), e `Descricao` rejeita caracteres de controle, o que também fecha injeção em log.

### A06 — Design inseguro

O isolamento por comerciante é requisito de desenho, com verificação em todas as camadas (endpoint →
contexto → filtro de consulta → restrição de banco), e não uma checagem no controlador. A idempotência
trata um caso de abuso explicitamente: chave reaproveitada com conteúdo diferente recebe `409`, em vez
de sobrescrever silenciosamente — a impressão SHA-256 do conteúdo existe para isso. O outbox
transacional garante que nenhum evento seja publicado sem o lançamento correspondente commitado.

### A07 — Falhas de autenticação

Fluxo padrão, não caseiro: `client_credentials` com `client_secret_basic`, validado pela biblioteca
de JWT da plataforma sobre metadados de descoberta e JWKS. A validação de claim é completa — emissor,
audiência, validade e assinatura, todos ativos, com `ClockSkew` reduzido de cinco minutos para trinta
segundos e `MapInboundClaims` desligado (`AutenticacaoExtensions`). **Cliente inexistente e segredo
incorreto recebem resposta idêntica** — mesmo corpo, mesmo status, mesmo cabeçalho `WWW-Authenticate`
—, de modo que não há enumeração de comerciantes; o cheat sheet destaca o status HTTP divergente como
o vazamento mais esquecido, e aqui ele é o mesmo. O emissor não sobe com cliente ausente, segredo
vazio ou `client_id` duplicado. Nenhum segredo e nenhuma credencial emitida aparece em log, garantido
por teste que falha se vazarem (`AusenciaDeSegredoNoLogTestes`).

### A08 — Falhas de integridade de software ou de dados

O dado que decide acesso é verificado por assinatura antes de virar decisão. Não há desserialização
insegura: apenas JSON sobre `record` de tipos fixos, sem resolução dinâmica de tipo. E não há
atribuição em massa — o contrato de entrada do registro tem quatro campos e **não** expõe o
comerciante, que só pode vir do token.

### A09 — Falhas de registro e alerta

Os três serviços exportam log, rastro e métrica por OpenTelemetry, em formato consumível por
ferramenta, com instrumentação de HTTP, banco e mensageria. Todo log de uma requisição autenticada
carrega o identificador do comerciante por escopo (`TelemetriaExtensions`), o que dá contexto
suficiente para análise posterior. Dado sensível fora do log é verificado por teste, não por
convenção. Injeção em log é mitigada na origem, pela rejeição de caracteres de controle na entrada.

### A10 — Tratamento incorreto de condições excepcionais

Categoria nova em 2025 e, por coincidência de desenho, bem coberta. Há um manipulador global único
por serviço, traduzindo exceção de domínio para status semântico sem expor estado interno
(`ExcecaoDeDominioParaProblemDetails`). Toda decisão de acesso **falha fechada**: contexto de
comerciante indefinido lança, em vez de consultar sem filtro. O rollback é completo — unidade de
trabalho no registro, transação explícita no consumo. A corrida na chave de idempotência é tratada, e
não ignorada: a violação concorrente de unicidade é capturada e resolvida devolvendo o registro
vencedor. A retentativa com intervalos crescentes e a fila de falha persistente (decisão
[004](004-retentativa-sem-plugin-do-broker.md)) impedem que uma mensagem ruim trave o consumidor ou
desapareça. Os serviços em segundo plano registram a exceção e sobrevivem, sem engolir cancelamento.

## O que não se aplica

| Recomendação | Por que não se aplica |
|---|---|
| MFA, força e rotação de senha, recuperação de senha | Autenticação é máquina-a-máquina; não há usuário humano nem senha escolhida por pessoa |
| Invalidação de sessão, identificador de sessão em cookie seguro, expiração por inatividade | Não há sessão: a credencial é um token de curta duração sem estado no servidor |
| Escape contextual de saída, política de conteúdo, defesa contra XSS | A aplicação não renderiza HTML; a única superfície HTML é a documentação interativa, tratada como lacuna abaixo |
| Desserialização de dado não confiável | Apenas JSON sobre tipos fixos, sem resolução dinâmica de tipo |
| Linguagem com segurança de memória | .NET já é, e não há bloco `unsafe` no código |

## Lacunas e evoluções futuras

Cada linha traz o gatilho — o que precisa acontecer para a evolução passar a se pagar. As três
primeiras são as únicas tratadas como dívida: custam pouco e fecham recomendações cobradas por mais
de uma categoria.

| Lacuna | Categorias | Gatilho |
|---|---|---|
| **Falha de autenticação não é registrada** — o emissor não produz log algum em `invalid_client` | A07, A09 | Imediato: é pré-requisito para detectar qualquer ataque contra o emissor |
| **Sem limite de taxa** em nenhum dos três serviços | A01, A07, A10 | Imediato para o endpoint de token (força bruta ilimitada); por escala para os demais, conforme a escada de gatilhos do `README` |
| **Documentação interativa exposta sem condição de ambiente** nos três serviços | A02 | Imediato: é superfície HTML e inventário de endpoints publicados fora de desenvolvimento |
| **Algoritmo de assinatura não restrito** na validação do token | A04, A07 | Imediato como defesa explícita; hoje a biblioteca já rejeita algoritmo incompatível com a chave |
| **`AllowedHosts` irrestrito**; **contêineres executando como root** | A02 | Primeira exposição fora da rede local |
| **Segredo de cliente em texto claro**, sem hash adaptativo, sem rotação nem expiração | A04, A07 | Primeiro ambiente com segredo que não seja semente de desenvolvimento |
| **Sem TLS, redirecionamento HTTPS, HSTS ou cabeçalhos de segurança** | A02, A04 | Primeira exposição fora da rede do `docker-compose`; hoje declarado no `README` |
| **Sem integração contínua**: nenhuma verificação de dependência vulnerável, nenhum portão de merge | A03, A05 | Segundo colaborador no repositório, ou primeira dependência com aviso de segurança publicado |
| **Sem arquivo de trava de dependência, sem inventário de componentes, imagens sem digest fixo** | A03, A08 | Necessidade de reproduzir um build antigo, ou primeira auditoria de fornecedor |
| **Chave de assinatura gerada no primeiro início, em arquivo sem proteção em repouso, sem rotação** | A04 | Primeiro ambiente não descartável — a decisão [005](005-emissor-sem-banco-e-ausencia-de-revogacao.md) registra o porquê do desenho atual |
| **Credencial sem identificador próprio e sem revogação** | A07 | Registrado e aceito na decisão [005](005-emissor-sem-banco-e-ausencia-de-revogacao.md): a validade de quinze minutos é o único mecanismo de encerramento |
| **Mensagem no broker não é assinada**, e o consumidor confia no comerciante declarado no evento | A08 | Broker compartilhado com produtor fora deste sistema; hoje a contenção é a credencial do próprio broker |
| **Sem retenção, limiar ou alerta de telemetria**; sem plano de resposta a incidente | A09 | Registrado na decisão [006](006-aspire-dashboard-sobre-stack-lgtm.md); o gatilho é uma investigação que precise alcançar intervalo já perdido |
| **Sem trilha de auditoria com garantia de integridade** | A09 | Exigência de auditoria externa sobre o histórico de lançamentos |
| **Sem retentativa de conexão ao banco, disjuntor ou anteparo** | A10, resiliência | Primeira indisponibilidade transitória de banco observada em ambiente compartilhado |
| **Sem limite de negócio por comerciante** (teto de valor ou de volume diário) | A06 | Primeiro caso de uso com risco financeiro por volume, hoje inexistente |

## Riscos emergentes

A página "Próximos Passos" do Top 10:2025 aponta três riscos fora da lista principal. Dois já estão
cobertos acima — resiliência de aplicação aparece na tabela, e segurança de memória é propriedade da
plataforma. O terceiro é **confiança indevida em código gerado por IA**, e a resposta aqui é de
processo, não de código: todo código passa por revisão humana e pelos mesmos portões automáticos do
restante do repositório — avisos tratados como erro e analisador de qualidade aplicados a todos os
projetos por `Directory.Build.props`, mais a suíte de testes. A responsabilidade pelo que é commitado
é de quem commita, independentemente da origem do rascunho.

## Referências

Documentos que fundamentaram a comparação, todos consultados em 12 de setembro de 2026:

| Referência | Endereço |
|---|---|
| OWASP Top 10:2025 — índice | <https://top10.owasp.org/2025/pt-BR/> |
| Introdução e metodologia | <https://top10.owasp.org/2025/pt-BR/0x00_2025-Introduction/> |
| Estabelecendo um programa moderno de segurança de aplicações | <https://top10.owasp.org/2025/pt-BR/0x03_2025-Establishing_a_Modern_Application_Security_Program/> |
| A01 — Controle de Acesso Quebrado | <https://top10.owasp.org/2025/pt-BR/A01_2025-Broken_Access_Control/> |
| A02 — Configuração Insegura | <https://top10.owasp.org/2025/pt-BR/A02_2025-Security_Misconfiguration/> |
| A03 — Falhas na Cadeia de Suprimentos de Software | <https://top10.owasp.org/2025/pt-BR/A03_2025-Software_Supply_Chain_Failures/> |
| A04 — Falhas Criptográficas | <https://top10.owasp.org/2025/pt-BR/A04_2025-Cryptographic_Failures/> |
| A05 — Injeção | <https://top10.owasp.org/2025/pt-BR/A05_2025-Injection/> |
| A06 — Design Inseguro | <https://top10.owasp.org/2025/pt-BR/A06_2025-Insecure_Design/> |
| A07 — Falhas de Autenticação | <https://top10.owasp.org/2025/pt-BR/A07_2025-Authentication_Failures/> |
| A08 — Falhas de Integridade de Software ou de Dados | <https://top10.owasp.org/2025/pt-BR/A08_2025-Software_or_Data_Integrity_Failures/> |
| A09 — Falhas de Registro e Alerta de Segurança | <https://top10.owasp.org/2025/pt-BR/A09_2025-Security_Logging_and_Alerting_Failures/> |
| A10 — Tratamento Incorreto de Condições Excepcionais | <https://top10.owasp.org/2025/pt-BR/A10_2025-Mishandling_of_Exceptional_Conditions/> |
| Próximos Passos (riscos emergentes) | <https://top10.owasp.org/2025/pt-BR/X01_2025-Next_Steps/> |
| OWASP Authentication Cheat Sheet | <https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html> |
