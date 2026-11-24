# Notas do desenvolvedor (F14p, etapa 4)

Premissas técnicas do F14p (F14-50, F14-60, F14-70; D9, D11, D14; ADR 0010, 0014 item 6). Nenhuma muda cenário nem decisão do design.

## Ambiente

- Sessão sem SDK .NET: nada de C# foi compilado nem rodado aqui. Domain, Application, Infrastructure, Api e os testes .NET só rodam na CI. Assinaturas conferidas por leitura do código.
- Sem migration (nenhuma tabela nem coluna nova) e sem mudança no OpenAPI (os corpos e as respostas são os mesmos; só há uma mensagem nova num `400` que já existia). Sem `pnpm api:tipos`, sem front.

## Lista de domínios (D9 a)

- **Origem:** `https://github.com/disposable-email-domains/disposable-email-domains`, arquivo `disposable_email_blocklist.conf`, **commit `9cf2bee345b0276e1b65cee7484beb51e5c715b1`** (o `main` em 24/11/2026, conferido por `git ls-remote`; o arquivo baixado do `main` e o do commit têm o mesmo SHA-256, `00c87dbc…a575`). 10.455 domínios, todos em minúsculas, um por linha, sem linha vazia; 10 em IDN (`xn--`).
- **Licença:** CC0 1.0 Universal (domínio público), pelo `LICENSE.txt` do repositório. **O PR precisa citar a licença.** CC0 não exige atribuição; a origem fica no cabeçalho do arquivo assim mesmo.
- **Onde:** `src/Acordu.Infrastructure/Seguranca/dominios-descartaveis.txt`, com 4 linhas de cabeçalho (`#`: o que é, a origem, o commit, a licença e como atualizar) e a lista copiada sem mudança. Embutido no assembly da Infrastructure (`EmbeddedResource` com `LogicalName` fixo no `.csproj`): não depende de caminho no disco nem de variável, e entra na imagem (o `Dockerfile` copia `src/`).
- **Atualizar:** trocar as linhas abaixo do cabeçalho pelo arquivo novo e o commit no cabeçalho, num PR `melhoria` (segurança §4). Os testes de `DominiosDescartaveisTests` conferem que a lista carrega, tem mais de 10 mil domínios e não pega os provedores comuns (Gmail, Outlook, Hotmail, Yahoo, UOL, BOL, iCloud) nem os domínios dos testes e da demonstração. Conferido aqui, contra a lista baixada: nenhum domínio de e-mail usado nos testes .NET, no front, na E2E e no bot do manual está nela (nem como pai).

## Comparação

- Porta `Application.Cadastro.IDominiosDescartaveis.EhDescartavel(email)`; implementação `Infrastructure.Seguranca.DominiosDescartaveis` (singleton, `FrozenSet` ordinal, lida uma vez do recurso).
- Compara o domínio depois do último `@` e cada domínio-pai (`a.b.x.com` → `b.x.com` → `x.com`), parando antes do sufixo solto (a lista não tem TLD sozinho; `ana@test` não é conferido). Sem diferenciar maiúsculas, sem o ponto final. Domínio com acento vai para a forma ASCII (`IdnMapping`, ICU está na imagem) antes de comparar, porque a lista guarda os IDN em `xn--`; se a conversão falhar, compara como veio (só não vai coincidir).
- Mensagem única, `MensagensDeCadastro.EmailDescartavel` ("Use um e-mail que você vai continuar usando: endereços de e-mail temporário (descartável) não são aceitos."), no campo `email`, pelo `MensagensDeCadastro.RecusarDescartavel` usado nos dois lugares.

## Onde a recusa entra

- **Cadastro** (`ServicoAutenticacao.RegistrarAsync`): logo depois do formato do e-mail (`Email.Normalizar`), no bloco do que não consulta o banco, antes da senha, do documento, do aceite e do convite (o gancho que a nota do F14n deixou). Vale **com ou sem convite e em qualquer modo** (F14-60 não tem exceção): no modo convite sem convite, o descartável recebe o `400` do e-mail, não o do convite; com convite, o convite nem é consultado (nada é gasto).
- **Lista de espera** (`InscricaoListaEspera`): depois do `EntradaListaEspera.Registrar` (os campos na ordem do formulário vêm antes; o primeiro erro é o que volta), antes de gravar. A resposta do descartável é `400`, não o `202` do F14-53: o `202` igual protege quem já está na lista, e um domínio descartável não revela nada sobre ela.
- **Fora (premissa, para o Daniel):** a emissão de convite de empresa na área não confere a lista. A Nebrys escolhe o e-mail; se escolher um descartável, o cadastro com esse convite é recusado no e-mail. Não está nos cenários; ficou em "Para o Daniel confirmar" no `estado.md` como melhoria pequena. Convites da equipe e do portal também não passam pela lista (RF-AUT-10 fala só de cadastro e lista de espera).

## F14-50 (modo aberto)

- Nenhum código: o caminho já passava desde o F14n. Os testes sobem a Api com `Cadastro__Modo=aberto` explícito (sem depender do padrão da fixture) e conferem o `201` como antes do F14 (TESTE, Dono, sessão, confirmação de e-mail enviada, conta sem e-mail confirmado), o documento registrado e o aceite gravado, e cada regra do F14 recusando no campo (sem documento, documento inválido, documento que já teve teste, e-mail descartável, sem aceite, aceite de outra versão).

## F14-70 (trilha da área)

- As ações já gravavam a trilha (F14l, F14n, F14o). Para o "antes e depois" valer em todas do mesmo jeito, **a emissão do convite passou a gravar `antes: null`** e **o descarte da entrada, `depois: null`** (antes as chaves faltavam). O resto da linha não mudou. Contrato §17, segurança §8 e convenções §3 dizem o formato comum.
- "A empresa ou o e-mail afetado": empresa no alvo (`alvo_tipo = empresa`, `alvo_id`) nas ações de situação; e-mail em `dados.email` nas de convite e lista de espera. A **liberação de documento** não tem nenhum dos dois, de propósito (D14: o documento e o HMAC nunca vão para a trilha); o teste confere que a linha não tem o documento nem o hash.
- `TrilhaNebrysTests` (funcional): faz cada uma das 10 ações pela API, com uma foto das linhas `nebrys.*` antes, e confere na linha nova: sem tenant, o operador, o instante do relógio, o afetado e as chaves `antes` e `depois`, com os valores esperados de cada lado. Dois testes de cobertura: as constantes `nebrys.*` de `AcoesAuditoria` (menos as da sessão) e as rotas que não são `GET` do `openapi/nebrys.json` (menos a abertura da sessão) têm de ser exatamente as do teste. Ação ou rota nova da área sem caso quebra o teste (convenções §3). A demonstração (F14s) vai entrar aqui quando criar as ações dela.
- A sessão de operação (`nebrys.sessao`, `.falha`) fica fora dessa conferência: não é ação sobre empresa ou e-mail, e já tem os testes do F14k.

## Documentos

Contrato §2 (e-mail descartável no registro; modo aberto com todas as regras), §16 (lista de espera) e §17 (formato comum da trilha); segurança §4 (o "Fora, por ora" da lista descartável saiu; regra, origem e atualização da lista) e §8 (conferência da trilha); convenções §2 (e-mails: a porta dos descartáveis) e §3 (trilha da área e o teste de cobertura); estrutura do repositório (o arquivo em `Infrastructure/Seguranca`); estado.md (em revisão, próximos passos, `CADASTRO_MODO` já criada, ponto para o Daniel). ERS sem mudança (RF-AUT-10 já diz isso). Modelo de domínio sem mudança (a lista não é do domínio). Fluxo do sistema (BPMN) sem mudança: nenhum passo, papel ou status novo.
