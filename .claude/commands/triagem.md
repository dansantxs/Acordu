---
description: Etapa 7 - transforma aviso do Discord, erro de produção, sugestão ou relato em issue rotulada
argument-hint: <texto colado, id do Error Reporting ou relato>
---

Etapa 7 da fábrica (`docs/processo.md` §3.7, rótulos no §7). Entrada: **$ARGUMENTS**.

1. Leia `docs/processo.md` §3.7 e §7. Classifique a entrada:
   - aviso de #alertas, id do Error Reporting (com `gcloud` logado: `gcloud beta error-reporting events list --service=acordu-api` e o grupo), queda ou job falho → **produção**;
   - texto de #sugestoes ou pedido de quem usa → **sugestão**;
   - relato de comportamento errado → **bug**.
2. **Sem dado pessoal:** nada de nome, e-mail, telefone, documento, texto livre de cliente do prestador ou nome da empresa na issue. Troque por "uma empresa", "um contato". Ids técnicos (rota modelo, tipo da exceção, versão, quadro) podem ir.
3. **Procure igual antes de criar:** `gh issue list --state all --search "<tipo da exceção ou rota ou palavras-chave>" --json number,title,state,labels` (ou `gh api "search/issues?q=repo:{owner}/{repo}+<termos>"`). Achou aberta: comente com a ocorrência nova (data, versão, contagem) e pare. Achou fechada e é o mesmo erro: reabra com o comentário (é regressão) e diga isso.
4. Crie a issue (`gh issue create --title … --label … --body-file …`):
   - **bug** (`bug`, mais `producao` se veio da produção): o que acontece, onde (rota modelo, tela, versão), desde quando, e o **cenário de regressão** no formato dos cenários (processo §3.2), que o PR de correção vai cobrir com teste (`Closes #N`, sem spec);
   - **sugestão** (`sugestao`): o pedido nas palavras de quem pediu, sem identificação, e de onde veio (#N da sugestão);
   - pequena e clara, um PR: `melhoria`; do tamanho de um marco (pede spec): `marco`, que vai para o `/roadmap` (G1);
   - não deu para classificar: `triagem`, com a dúvida no corpo.
5. Termine com o número e os rótulos. Não comece a correção: a prioridade é do Daniel (G1). Se for erro grave em produção (fora do ar, dado errado), diga isso no topo da resposta.
