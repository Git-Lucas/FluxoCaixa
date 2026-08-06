# 002 — Estratificação em camadas só no Lançamentos

## Contexto

O Lançamentos e o Consolidado são serviços de complexidade muito diferente. O primeiro tem
invariantes de negócio reais — valor estritamente positivo, competência dentro de uma janela de 90
dias, tipo de lançamento — que precisam ser impossíveis de contornar. O segundo não tem domínio: a
única operação é somar um valor já validado a um agregado por dia.

## Decisão

O Lançamentos é dividido em quatro projetos, com dependência sempre apontando para dentro:

```
Api ──────────▶ Aplicacao ──────────▶ Dominio
 │                  ▲                    ▲
 └──▶ Infraestrutura ┘────────────────────┘
```

`Dominio` concentra a entidade `Lancamento` e os value objects que validam suas próprias
invariantes no construtor — impossíveis de instanciar em estado inválido. `Aplicacao` expõe o caso
de uso e as portas que a infraestrutura implementa.

O Consolidado e o Identidade são, cada um, um projeto único, sem camada de domínio separada:

```
FluxoCaixa.Consolidado.Api          FluxoCaixa.Identidade.Api
├── Consulta/                       ├── Chave/
├── Consumo/                        ├── Emissao/
├── Persistencia/                   ├── Descoberta/
└── Program.cs                      └── Program.cs
```

## Por que o Consolidado não replica a estratificação

O valor, a competência e o tipo do lançamento já foram validados no registro; revalidá-los no
consumo duplicaria uma regra que já tem dono. Uma camada de domínio sem invariante para proteger é
indireção sem contrapartida.

**Alternativa considerada: espelhar as quatro camadas do Lançamentos, por simetria.** Descartada.
Simetria entre serviços de complexidade diferente é custo sem retorno — a estratificação existe para
proteger invariante, e aqui não há invariante a proteger.

## Por que o Identidade não replica a estratificação

O emissor de credenciais não tem domínio no sentido de regra de negócio a proteger: ele autentica um
cliente contra uma lista configurada e assina um token. A separação por pasta (`Chave/`, `Emissao/`,
`Descoberta/`) já isola as responsabilidades sem precisar de projetos e portas adicionais.

**Alternativa considerada: mesma estratificação do Lançamentos.** Descartada pelo mesmo motivo do
Consolidado — cerimônia sem invariante para proteger.
