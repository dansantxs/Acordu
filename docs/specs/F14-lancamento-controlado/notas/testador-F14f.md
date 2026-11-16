# Notas do testador (F14f, etapa 5)

Parecer: aprovado com ressalvas.

- F14-78: `legal.test.tsx` abre `/privacidade` e `/termos` sem sessão e confere versão e vigência (lidas da constante) e que não vai ao login; também com a API fora do ar.
- F14-84: entrar (política e termos), aceite de convite do portal, portal com conta e portal por link (só política); nenhuma tem checkbox.
- D10: `versoes.test.ts` confere versão e vigência do arquivo contra a constante e a impressão do corpo; mudar o texto sem a constante falha (mensagem mostra a impressão nova). Normaliza CRLF.
- Novo `prazos.test.ts`: canal `contato@acordu.app.br` e prazos (teste 30 dias, expurgo 30 dias, backups 14 dias, trilha 12 meses na política) nos textos.
- F14-85/86: manuais (revisão do Daniel, de preferência com advogado, antes do merge).

Ressalvas para a descrição do PR:
- Os textos têm marcadores `[a preencher pelo Daniel]` (razão social, CNPJ, base legal, foro etc.) que aparecem na página; nenhum teste os cobra, e eles precisam sumir antes de publicar.
- A vigência `2026-11-16` é provisória (Daniel ajusta no arquivo e na constante).
- Os textos citam partes futuras do F14 (F14g, h, i, j, n, o); decisão do Daniel.

## Rodada 1

Parecer: aprovado com ressalvas.

- F14-78 e F14-84 seguem verificados (nomes na coluna Teste inalterados). Os links do pé são conferidos com o nome acessível "... (abre em outra aba)", o que cobre o aviso ao leitor de tela.
- `PaginaTermos_SaiDaPagina_RestauraOTituloDaAba` cobre a restauração do `document.title`.
- `prazos.test.ts` e `versoes.test.ts` seguem verdes.
- A lista do navegador em `privacidade.md` §10 bate com o código: tema e couro, novidades vistas, notificações do navegador e marcas, lembrete do cronômetro, primeiros passos (localStorage); tokens de link `convite`, `convite-portal`, `senha`, `confirmacao` (sessionStorage). Sem defeito.
- Ressalvas anteriores seguem: marcadores `[a preencher pelo Daniel]` e `[a confirmar ...]` ainda aparecem nas páginas e precisam sumir antes de publicar (nenhum teste os cobra); vigência provisória; referências a partes futuras do F14.
