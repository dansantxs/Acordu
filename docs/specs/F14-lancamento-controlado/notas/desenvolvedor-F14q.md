# Notas do desenvolvedor (F14q, etapa 4)

Premissas técnicas do F14q (F14-44, F14-46, F14-109; D11, D12; telas sobre a API do F14n, F14o e F14p). Nenhuma muda cenário, decisão do design nem a API. Só front: sem migration, OpenAPI, tipos regerados nem variável nova.

## Cadastro (telas públicas)

- **Modo pela API (D11):** `/cadastro` (`features/sessao/TelaCadastro.tsx`) lê `GET /auth/cadastro` (`consultaModoCadastro`, cache enquanto a página está aberta: o modo só muda com deploy). `ABERTO` → o cadastro de sempre; `CONVITE` → o formulário da lista de espera no lugar, com o motivo no subtítulo da capa (F14-44). Enquanto carrega, "Carregando…".
- **Sem resposta do modo** (API fora do ar, rede): a tela mostra o cadastro e a API decide no envio. A recusa em `errors.convite` sem convite (o `400` do modo convite, contrato §2) troca a tela para a lista de espera. Premissa: preferi não ter uma tela de erro própria, e isso também deixa os testes antigos do cadastro (que não simulam a rota nova) iguais.
- **Rotas:** `_acesso/cadastro.tsx` virou `_acesso/cadastro/index.tsx` (o arquivo plano viraria layout das rotas filhas sem `Outlet`), com `cadastro/convite/$token.tsx` e `cadastro/convite/index.tsx`, no padrão dos outros links: o token sai da URL na hora (`retirarTokenDaUrl`, tipo novo `convite-empresa` em `tokenDaUrl.ts`) e fica na `sessionStorage` da aba até o cadastro passar.
- **Cadastro por convite (F14-46):** `CadastroPorConvite` consulta `POST /auth/registro/convite/consulta`; o `FormularioCadastro` ganhou `convite` (token e e-mail): o e-mail fica num campo só leitura com o valor do convite (vai no corpo o e-mail da consulta, não o do campo) e o token vai em `convite`. A recusa da API em `errors.convite` aparece no topo (não há campo para ela). Sucesso apaga o token.
  - A rota do convite **não** consulta o modo (o convite vale nos dois, contrato §2) e **não** redireciona quem já tem sessão (o `/cadastro` redireciona): quem abre o link logado numa empresa ainda vê o convite; ao cadastrar, a sessão passa a ser a da conta nova.
  - Token que não vale (`401`) ou ausente na aba: "Este convite não vale mais…", com o canal da Nebrys e o link "Ir para o cadastro" (no modo convite, a lista de espera).
  - **E-mail que já tem conta** (`temConta`, F14-105, decisão 18 do G2): a tela explica e não mostra o formulário, porque a API recusaria o e-mail em uso; oferece "Entrar com a conta que já existe" e "peça um convite para outro e-mail".
- **Lista de espera:** `FormularioListaEspera` com nome, e-mail, nome da empresa e mensagem opcional (até 1.000, `maxLength`; vazia vai `null`). A confirmação é a mesma para entrada nova e repetida (a API responde `202` igual, F14-53) e não promete prazo. `429` diz que o limite é por hora; `400` fica no campo (inclusive o e-mail descartável do F14p).
- **Turnstile:** `DesafioTurnstile` ganhou `acao` (`registro` padrão, `lista-espera` na lista), a mesma ação da rota (convenções §3; o token de uma não vale na outra).
- **F14-109:** a tela da lista de espera usa a `CapaAcesso` com `soPrivacidade`: só o link da política no pé, sem os termos (quem só entra na lista não usa o Acordu) e sem caixa de aceite. Com os textos não publicados, o link some, como nas outras telas (D16; teste `listaEsperaEscondido.test.tsx`).
- **E2E:** sem teste novo. A E2E sobe a API em Development (`Cadastro__Modo=aberto`): a fumaça segue passando pelo cadastro aberto. O modo convite e o convite precisariam de uma operadora da Nebrys configurada na E2E; os testes de tela cobrem os dois caminhos com a API simulada.

## Área da Nebrys

- Telas novas em `src/nebrys`: `ListaConvites` (`/nebrys/convites`, emitidos por último primeiro, "Novo convite" numa janela), `DetalheConvite` (`/nebrys/convites/{id}`, Reenviar e Revogar com confirmação) e `ListaEspera` (`/nebrys/lista-espera`, Convidar e Descartar com confirmação; "Ver o convite" leva ao detalhe pelo `conviteId`). Links na faixa da casca.
- Ações do contrato §17, sem `If-Match` (a API não pede para convites e lista). `422` mostra o `detail` na janela; `404` de um item fecha a janela, relê e avisa que não existe mais, sem encerrar a sessão (só as listas recebem o `404` da barreira e encerram, como no F14m); `409` da lista de espera relê a lista e avisa no topo; `emailEnviado: false` avisa para reenviar. Reenviar e revogar ficam no pendente e no vencido (o usado e o revogado não têm ações).
- Datas em `America/Sao_Paulo`; os convites mostram também a hora (vencem na hora exata do envio + 7 dias).
- **Ressalvas do F14m tratadas, por tocar no mesmo código:**
  - `retry: 1` global repetia o `404`: as consultas da área passaram a usar `repetirConsulta` (`src/nebrys/api.ts`), que repete uma vez só falha de rede ou `5xx`, nunca `4xx` nem a sessão encerrada. O padrão global do app (`app/Aplicacao.tsx`) não mudou: fora do escopo da área.
  - `sessaoOperacao()` na renderização da `CascaNebrys` podia encerrar a sessão e avisar os ouvintes no meio da renderização: a casca passou a usar `lerSessaoOperacao()` (novo, sem efeito). O temporizador e o cliente seguem encerrando no prazo.
- A emissão continua aceitando e-mail descartável (decisão de 24/11, F14p): nada mudou aqui.

## Manual, Novidades e capturas (o que pedir ao bot)

- Seção nova "Cadastro, convite e lista de espera" (`conceitos`), com duas capturas novas, e nota de Novidades de 24/11 (equipe). A área da Nebrys continua fora do manual, das capturas e das Novidades.
- Capturas novas em `features/ajuda/capturas.ts` e receitas em `apps/web/ajuda/capturas.captura.ts`:
  - `cadastro-convite`: `/cadastro/convite/token-da-foto`, pronto quando aparece "A Nebrys convidou"; a consulta do convite é simulada (`page.route`) com um e-mail fictício (`ana.souza@exemplo.com`), porque o bot não emite convite;
  - `lista-espera`: `/cadastro`, pronto com o título "Lista de espera"; o `GET /auth/cadastro` é simulado como `CONVITE`, porque o bot sobe a API no modo aberto.
  - As duas rodam num contexto sem sessão, uma aba nova por captura (`anonimo`, `simular`).
- **Pedido ao bot:** `.github/ajuda-request` criado na árvore de trabalho (o orquestrador faz o commit). **Até o bot gravar `public/ajuda/cadastro-convite.webp` e `lista-espera.webp`, o teste `capturas.test.ts › toda captura listada tem o arquivo gerado pelo bot` falha** (é o que o teste cobra). As outras capturas são refeitas iguais (nenhuma tela delas mudou).

## Documentos

Convenções §4 (cadastro pelo modo da API, tokens de link, ações e `repetirConsulta` da área); estrutura do repositório §6 (rotas do cadastro e telas da área); contrato §2 (a SPA tira o token do convite da URL e fixa o e-mail); `estado.md` (em revisão e o próximo passo: com o F14q na `main`, basta trocar `CADASTRO_MODO` para `convite` e fazer um deploy). Spec: coluna Teste de F14-44, F14-46 e F14-109. ERS, domínio, read side, segurança, OpenAPI e BPMN sem mudança (nenhum passo, papel ou status novo; `gerar-fluxo-bpmn.py --conferir` sai 0).

## Rodada 1 (ressalvas do revisor)

- Teste novo `CadastroPorConvite_ComSessaoAtiva_MostraOFormularioEOCadastroTrocaASessao` (`cadastroConvite.test.tsx`): com sessão ativa, o formulário do convite aparece e o cadastro troca o token da sessão; frase em convenções §4; coluna Teste do F14-109 com o `listaEsperaEscondido.test.tsx`; `SUBTITULO_CADASTRO` exportado e reaproveitado na rota do convite.
- **Ponto para o Daniel (comportamento não mudado):** no ramo `temConta`, o link "Entrar com a conta que já existe" vai a `/entrar`, que manda quem já tem sessão para `/` (`seguirSeJaEntrou`). Quem abre o link logado com outra conta cai na sessão atual, sem chance de entrar com a conta do e-mail do convite (teria de sair antes). Opções: deixar assim, ou o link sair da sessão antes de ir ao `/entrar`.
