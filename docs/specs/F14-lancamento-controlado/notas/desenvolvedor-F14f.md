# Notas do desenvolvedor (F14f, etapa 4)

Premissas técnicas do F14f. Nenhuma muda cenário nem decisão do design. Sem mudança na API (nem OpenAPI, nem tipos).

## Premissas

- **Onde ficam os textos (D10):** `apps/web/src/features/legal/textos/termos.md` e `privacidade.md`, lidos por `?raw` do Vite e interpretados por `lerTextoLegal` (`textosLegais.ts`): cabeçalho `---` com `versao` e `vigencia`, e um subconjunto de Markdown (`#` a `###`, parágrafos, listas com `-`, `**negrito**`, `[texto](endereço)`). Sem biblioteca nova e sem HTML cru (`TextoMarcado` monta elementos do React), então nada muda na CSP (nem `style` inline).
- **Versão e o teste (D10):** a constante é do SPA neste PR (`VERSOES_TEXTOS_LEGAIS`, `features/legal/versoes.ts`), com `versao`, `vigencia` e uma `impressao` (FNV-1a de 32 bits do corpo, quebras de linha normalizadas). O `versoes.test.ts` confere versão e vigência do arquivo contra a constante e a impressão do corpo contra a guardada: mudar o texto sem mexer na constante faz o teste falhar, e a mensagem mostra a impressão nova. Sem a impressão, o teste só conferiria dois números iguais e não pegaria texto mudado sem versão nova. Enquanto o texto não for publicado (revisão do Daniel neste PR), ajustar o texto muda só a impressão; depois da publicação, muda a versão e a vigência também. No F14h, a constante da API (`TextosLegais`, design D10) entra no OpenAPI como `enum` e o teste passa a comparar com ela.
- **Versão e vigência iniciais:** `versao: '1'` e `vigencia: '2026-11-16'` nos dois. A vigência é a data de hoje como provisória: **o Daniel ajusta para o dia do merge** (ou do deploy) depois da revisão, no arquivo e na constante (o teste cobra os dois).
- **Páginas públicas (F14-78):** rotas `/termos` e `/privacidade` na raiz (`src/routes/termos.tsx`, `privacidade.tsx`), fora do `_app`, do `_acesso` e do portal: não têm `beforeLoad`, não esperam a sessão nem chamam a API. O `iniciarSessao` da carga do app continua rodando em qualquer página (é do `Aplicacao`, comum a todas); a página não depende dele (o teste com a API fora do ar abre a página). Não tirei essa renovação das páginas legais: exigiria mexer na partida da sessão (`aguardarSessao` começaria a sessão sob demanda) para poupar uma chamada que já existe em toda carga. Fica como melhoria se o custo do Cloud Run acordado pela visita direta às páginas aparecer.
- **Links (F14-84):** `LinksLegais` (`features/legal/LinksLegais.tsx`), num `nav` "Textos legais", abrem em outra aba (não perde o que foi digitado no formulário ao lado; `rel="noopener"`).
  - `CapaAcesso` põe os dois links no pé de todas as telas de acesso (entrar, cadastro, esqueci a senha, senha nova, confirmação, convite da equipe). O cadastro e o convite da equipe ganham a caixa de aceite e os links junto dela no F14h; os do pé ficam.
  - Telas do contato: aceite de convite do portal, tela do link sem sessão (`TelaLink`) e o pé da `CascaPortal` (conta e link) mostram **só a política** (`soPrivacidade`), porque os termos são entre a Nebrys e o prestador; a relação do contato é com o prestador (decisão 12 do G2). É o "onde cabe" do F14-84.
  - Nenhuma dessas telas pede aceite (os testes conferem que não há caixa de marcar).
- **Canal no texto:** os textos citam `contato@acordu.app.br` literalmente (é texto legal publicado e versionado, não configuração). A página em si não repete o endereço fora do texto, para não haver outra fonte do canal no código além do `VITE_CONTATO_NEBRYS` (convenções §4).
- **Fatos dos textos:** vêm só dos documentos do repositório: ERS 1.3.4 (best-effort, cold start, 30 s, 15 min), RF-AUT-06/07/09/11, RF-EMP-01 a 04, RNF-PRIV-01 a 03; ADR 0008 e 0014 (trilha de 12 meses, a da Nebrys sem tenant); ADR 0010 e deploy-ci §8 (Cloud Run `us-east4`, Neon `aws-us-east-1`, R2, Resend, backup diário pelo Actions com 14 dias); ADR 0012 (Cloud Logging, Error Reporting, Discord); segurança §6 e §9 (ViaCEP, BrasilAPI, Turnstile, cookie de sessão, `localStorage`); contrato (notificações e atividade de 90 dias, convites de 7 dias); decisões 1, 2, 3, 8, 9, 10, 12, 14 e 16 do G2. Nada copiado de política de terceiros.
- **Partes futuras do marco citadas nos textos:** lista de espera (F14o), convite de empresa (F14n), aceite e "ciente" (F14h), avisos por e-mail (F14j), expurgo da empresa (F14i), hash do documento (F14g). O texto descreve o produto do F14 inteiro; se o F14f for publicado antes desses PRs, a política descreve o que ainda vai existir. **O Daniel decide** se publica assim (o mais simples: o deploy do F14 inteiro vai junto) ou se retira as menções até lá (o que muda a versão depois).

## Marcadores `[a preencher pelo Daniel]` (aparecem na página como estão)

Política de privacidade:
1. Razão social da Nebrys (§1).
2. CNPJ da Nebrys (§1).
3. Endereço da Nebrys (§1).
4. Encarregado (DPO): nome, ou a indicação de dispensa como agente de pequeno porte (§1).
5. Base legal da LGPD de cada finalidade (§4), com o advogado.
6. Mecanismo de transferência internacional (art. 33 da LGPD) (§6), com o advogado.

Termos de uso:
7. Razão social, CNPJ e endereço da Nebrys (§1).
8. Condições comerciais da conta ativa (§5): preço, pagamento, reajuste, ou "combinadas caso a caso".
9. Se a Nebrys pode cancelar ou suspender por conta própria, com que aviso e efeito (§8), com o advogado.
10. Limitação de responsabilidade da Nebrys (§10), com o advogado.
11. Foro (§14).

## Rodada 1 (devolução do revisor)

Redação proposta, **não decisão do repositório**: cada trecho abaixo ficou no texto, seguido do marcador `[a confirmar pelo Daniel, com o advogado]`, para o Daniel manter, mudar ou tirar.

Termos de uso:
12. §3: "Os Donos da conta respondem pela escolha de quem entra na equipe, dos papéis e dos acessos dados aos contatos."
13. §10: o prestador "responde pelo conteúdo que a equipe e os contatos dele colocam no Acordu … e por usá-lo de forma lícita".
14. §10: os usos proibidos (mensagens indesejadas, conteúdo ilícito, acessar dados de outras empresas, testar a segurança, sobrecarregar o serviço).
15. §10: "não cria contas ou documentos diferentes para repetir o período de teste" (a regra de um teste por documento é da RF-AUT-09; a proibição como cláusula é proposta).
16. §11: "As sugestões de melhoria … podem ser usadas pela Nebrys para melhorar o Acordu."

Política de privacidade:
17. §5: "A Nebrys não vende dados pessoais."
18. §9: em incidente relevante, "a Nebrys avisa os prestadores afetados e a ANPD".

Fatos que o repositório não documenta viraram `[a confirmar pelo Daniel: …]`: região do Cloud Logging e do Error Reporting (§5); onde a Cloudflare guarda os arquivos do R2 e por onde passa a borda (§5 e §6). A §6 agora afirma só o documentado: o sistema (Cloud Run `us-east4`) e o banco (Neon `aws-us-east-1`) nos EUA (ADR 0010).

Mantido sem marcador: "a Nebrys escolhe quem convidar" (termos §3), porque é texto da RF-AUT-08 ("a Nebrys escolhe quem convidar") e da decisão 5 do G2 (conversão manual da lista de espera).

Armazenamento no navegador (política §10), conferido com grep de `localStorage`/`sessionStorage` em `apps/web/src` (e o script de tema do `index.html`): tema e couro (`shared/tema/tema.ts`), novidades vistas (`features/novidades/vistas.ts`), escolha de notificações e marcas de "já mostrada" (`shared/notificacoes/navegador.ts`), lembrete do cronômetro (`shared/cronometro/lembrete.ts`), dispensa dos primeiros passos (`features/hoje/PrimeirosPassos.tsx`) e, na `sessionStorage`, os tokens dos links até serem usados (`shared/sessao/tokenDaUrl.ts`).

Também nesta rodada: os links do `LinksLegais` têm "(abre em outra aba)" só para leitor de tela (`sr-only`); a `PaginaLegal` devolve o título anterior da aba ao sair (teste `PaginaTermos_SaiDaPagina_RestauraOTituloDaAba`). A impressão dos dois textos mudou na constante (D10); a versão segue `1`, porque o texto ainda não foi publicado.

O canal `contato@acordu.app.br` fixo nos `.md` é intencional (decisão 14 do G2): é texto legal publicado e versionado, não configuração, e o `prazos.test.ts` do testador confere que ele está nos dois textos.

## Pontos para o advogado (além dos marcadores)

- Se o aceite de quem cadastra vale como aceite da empresa ("declara ter poderes", termos §1), inclusive para MEI e pessoa física com CPF.
- Se a lista de direitos do titular (política §8) e o aviso de incidente (§9) estão no tom certo, e se cabe prazo de resposta explícito.
- Se a nova versão "sem bloquear o uso" (decisão 12 do G2, só "ciente") basta para mudanças relevantes, ou se alguma mudança deve exigir aceite.
- Lista de espera: o texto diz que a entrada fica até a Nebrys convidar ou descartar; não há prazo para a entrada convidada (F14o não define). Se precisar de prazo, é requisito novo (volta ao G2).
- Registros de funcionamento e de erro (Cloud Logging, Error Reporting): o texto não dá prazo, porque o repositório não fixa um (fica o padrão do Google). Se o advogado pedir prazo, é configuração no projeto do Google e uma linha a mais na política.
- Discord como destinatário (avisos de operação e texto das sugestões, com o nome da empresa, que num MEI pode ser o nome da pessoa; segurança §6).
- "As sugestões podem ser usadas pela Nebrys para melhorar o Acordu" (termos §11): redação sobre cessão.

## Manual, Novidades e capturas

- Manual da equipe: seção de conceito "Termos de uso e política de privacidade" (`textos-legais`). Guia do portal: seção "Seus dados" (`privacidade`). Novidades: notas de 16/11 para a equipe e para o cliente.
- O pé do portal ganhou uma linha (o link da política), e as telas de acesso, o pé com os links. As capturas do portal (`portal-lista`, `portal-detalhe`, `portal-nova`, `portal-contratos`) podem mostrar o pé: **o orquestrador deve commitar `.github/ajuda-request`** para o bot refazer as capturas. Nenhuma captura é das telas de acesso. Não gerei captura.

## Documento corrigido a pedido do orquestrador

- `docs/setup-infra-acordu.md`, Parte D passo 4: caminho do painel novo do Cloudflare (conta › Computação › Serviço de hospedagem de e-mail › Encaminhamento de e-mails › Adicionar domínio › zona › Verificar e ativar; "Endereços de destino" no topo) e o passo anterior de apagar o MX nulo e o `v=spf1 -all` do raiz, mantendo `send.acordu.app.br` e `_dmarc`.
