# ADR 0017 — Nome do fornecedor por configuração e nomes internos neutros

- **Status:** Aceito (pedido do Daniel em 27/11/2026, fora de marco)
- **Data:** 27/11/2026
- **Contexto do projeto:** Acordu (textos da API e do SPA, textos legais, área de operação do ADR 0014)
- **Complementa, sem mudar o texto:** ADR 0010 (configuração só por variável de ambiente) e ADR 0014 (área de operação).

## Contexto

O Acordu é da Nebrys, mas o nome não está fechado e o sistema pode ser vendido a outra empresa. Até aqui "Nebrys" está escrito à mão em dezenas de textos (recusas e e-mails da API, faixas, telas, ajuda, novidades, termos e privacidade) e no código: namespaces, classes, rotas (`/nebrys`, `/api/v1/nebrys`), o documento OpenAPI, chaves de configuração e ações da trilha. Trocar o nome hoje é uma busca e troca pelo repositório inteiro, com risco de esquecer um texto.

## Decisão

1. **Texto visível nunca escreve o nome do fornecedor.** Ele vem da configuração: `Fornecedor__Nome` (padrão `Nebrys`), `Fornecedor__Artigo` (`a`, o padrão, `o` ou `nenhum`) e `Fornecedor__Contato` (o canal, que já existia como `Nebrys__Contato`) na API; `VITE_NOME_FORNECEDOR`, `VITE_ARTIGO_FORNECEDOR` e `VITE_CONTATO_FORNECEDOR` no SPA. Em produção, uma variável só do GitHub para cada (`NOME_FORNECEDOR`, `ARTIGO_FORNECEDOR`, `CONTATO_FORNECEDOR`), conferida e normalizada no `deploy.yml` e passada aos dois lados com o mesmo valor (convenções §4, D16 do F14).
2. **Marcadores no feminino.** Os textos usam `{{fornecedor}}` (só o nome) e `{{a fornecedor}}`, `{{da fornecedor}}`, `{{à fornecedor}}`, `{{na fornecedor}}`, `{{pela fornecedor}}` (e as formas com maiúscula), trocados pelo artigo configurado: "à Nebrys", "ao Grupo Exemplo", "a Exemplo Ltda". Uma implementação de cada lado: `OpcoesFornecedor.Texto` (Application) e `textoDoFornecedor` (`shared/config/fornecedor.ts`). Marcador desconhecido é defeito do texto e falha; artigo inválido impede a API de subir e para o deploy.
3. **Textos legais com o marcador.** A impressão de versão (D10 do F14) é calculada sobre o corpo com os marcadores, igual em qualquer build. Trocar o fornecedor de um texto já publicado muda a parte contratante: é versão nova do texto, decidida com o advogado, não só a variável.
4. **Nomes internos neutros.** O código, as rotas e a configuração não levam o nome do fornecedor: a área do ADR 0014 é a **área de operação** (`/operacao`, `/api/v1/operacao`, `openapi/operacao.json`, `Application.Operacao`), e o que é do fornecedor nos textos é `Fornecedor`. Feito num PR próprio, com a migration que troca o prefixo das ações da trilha já gravadas.
5. **Documentos.** Os documentos vivos falam em "fornecedor" e "área de operação". ADRs aceitos, specs e notas de marcos fechados ficam como foram escritos (histórico), com "Nebrys" no sentido de hoje.

## Alternativas consideradas

- **Só o nome, sem o artigo:** "fale com a Nebrys" vira "fale com Grupo Exemplo", o que soa errado em português, ou os textos teriam que fugir do artigo em todo lugar.
- **Reescrever os textos sem citar ninguém** ("fale conosco", "nossa equipe"): perde a clareza de quem responde pelo serviço, que os textos legais e as recusas precisam ter.
- **Manter os nomes internos:** menos mudança agora, mas a URL `/nebrys`, o OpenAPI e a trilha continuariam com o nome antigo depois de uma troca.
