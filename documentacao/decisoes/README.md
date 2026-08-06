# Registros de decisão

Cada arquivo aqui registra uma decisão estrutural do sistema — uma decisão cuja reversão mudaria a
arquitetura, não apenas a implementação. Decisões pontuais de implementação não têm registro
próprio; ficam explicadas, quando precisam de explicação, junto ao código que as exige.

Cada registro apresenta o contexto que motivou a decisão, as alternativas consideradas e o motivo
do descarte de cada uma.

| # | Decisão |
|---|---|
| [001](001-outbox-transacional-e-assimetria-de-disponibilidade.md) | Outbox transacional e a assimetria "registrar é crítico, consultar é degradável" |
| [002](002-estratificacao-em-camadas-apenas-no-lancamentos.md) | Estratificação em camadas só no Lançamentos |
| [003](003-deduplicacao-manual-no-consolidado.md) | Deduplicação escrita à mão no Consolidado |
| [004](004-retentativa-sem-plugin-do-broker.md) | Retentativa com intervalos crescentes sem plugin do broker |
| [005](005-emissor-sem-banco-e-ausencia-de-revogacao.md) | Emissor sem banco, chave em volume nomeado, sem revogação |
| [006](006-aspire-dashboard-sobre-stack-lgtm.md) | Aspire Dashboard em vez da stack LGTM |
| [007](007-aptidao-ignora-o-transporte-de-mensagens.md) | Aptidão que ignora o transporte de mensagens |
| [008](008-fronteira-entre-metrica-e-log-por-cardinalidade.md) | Fronteira entre métrica e log pela cardinalidade |
