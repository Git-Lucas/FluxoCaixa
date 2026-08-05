# 005 — Emissor sem banco, chave RSA em volume nomeado, sem revogação

## Contexto

O Identidade emite credenciais assinadas para os comerciantes autenticarem contra o Lançamentos e o
Consolidado. O par de chaves RSA usado para assinar precisa existir em algum lugar antes da primeira
emissão, e precisa sobreviver a reinícios do serviço — gerar uma chave nova a cada início invalidaria
toda credencial em circulação.

O escopo do emissor é deliberadamente restrito: sem gestão de usuários, sem perfis granulares, sem
federação, sem nível produtivo. A pergunta orientadora do desenho foi "como o Keycloak faz" — ele
gera o par de chaves sozinho na criação do realm e o persiste no próprio banco, como componente do
realm, nunca em arquivo versionado.

## Decisão: chave gerada no primeiro início, persistida em volume nomeado

Sem banco no emissor. Na primeira inicialização, se não houver chave no caminho configurado, o
serviço gera um par RSA e o grava num volume nomeado do Docker — estado fora da imagem, gerado pelo
próprio processo, sem replicar o banco que o Keycloak usa para o mesmo fim.

| | PEM versionado no repositório | **Gerada no 1º início, volume nomeado** |
|---|---|---|
| Chave privada no controle de versão | Sim, mesmo rotulada dev-only | Não |
| Custo | Exceção no `.gitignore` + cópia na imagem | Geração condicional no início + volume |

**Alternativa considerada: chave PEM versionada no repositório**, para reprodutibilidade
determinística. Descartada: chave privada em controle de versão é o tipo de atalho que sobrevive ao
ambiente de desenvolvimento e vaza para produção por descuido, mesmo rotulada como exclusiva de
desenvolvimento.

**Alternativa considerada: geração efêmera a cada início.** Descartada por invalidar toda credencial
em circulação a cada reinício do contêiner, o que tornaria qualquer verificação de continuidade
confusa.

**Consequência aceita:** `docker compose down -v` apaga o volume junto com os bancos, invalidando
todas as credenciais emitidas até então. É o comando usual para zerar o ambiente, e agora carrega
esse efeito colateral.

## Decisão: sem revogação, introspecção nem renovação

O escopo do emissor exclui explicitamente lista de revogados, endpoint de introspecção e
`refresh_token`. A consequência é direta: **a validade da credencial é o único mecanismo que a
encerra.** Vazou, vale até expirar.

Isso torna o prazo de validade uma decisão de segurança, puxada por duas forças opostas: um prazo
curto reduz o alcance de um vazamento; um prazo longo tolera melhor o emissor ficar indisponível sem
os serviços de negócio pararem de aceitar credencial. Como a emissão é `client_credentials` — sem
interação humana —, não há custo perceptível em expirar rápido: o cliente apenas solicita de novo.
Por isso a validade escolhida é de 15 minutos.

**Alternativa considerada: lista de revogados consultada pelos serviços de negócio.** Resolveria o
vazamento, mas reintroduziria uma dependência síncrona do emissor no caminho de toda requisição
autenticada — exatamente o acoplamento que a validação local via JWKS existe para eliminar.
Descartada por contradizer o restante do desenho.
