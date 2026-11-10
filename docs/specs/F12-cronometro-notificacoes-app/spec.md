# Spec — F12: Cronômetro sem retrabalho, notificação do navegador e app instalável

| | |
|---|---|
| **Status** | Concluída |
| **Data** | 09/11/2026 |
| **Requisitos** | RF-APT-01, RF-APT-02, RF-NOT-03, RF-NOT-05, RNF-UX-01 (ERS) |
| **ADRs** | 0003 (horas úteis e expediente), 0004 (tempo em minutos), 0009 (tempo real, emenda do polling), 0010 (hospedagem) |

## 1. Objetivo

Quem aponta horas não perde tempo corrigindo o cronômetro:
- pode começar a contar a partir de um horário que já passou e ajustar a duração ao parar;
- pode iniciar a contagem já na criação da tarefa;
- é lembrado quando está no expediente sem nada rodando, ou quando o cronômetro ficou ligado.

Os avisos chegam como notificação do sistema operacional (o pop-up do Windows) mesmo com a aba minimizada, e o Acordu pode ser instalado como aplicativo, com ícone próprio.

## 2. Cenários de aceite

Hoje: `POST /cronometro/inicio` começa no instante da chamada, `POST /cronometro/parada` aponta o decorrido (arredondado, mínimo 1 min) na data de hoje, só há um cronômetro por pessoa (`409 cronometro-ativo`), e o sino é consultado a cada 60 s.

### Começar antes e ajustar ao parar

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F12-01 | pessoa com `apontamento.criar`, sem cronômetro | inicia informando "Começou às 08:40" (hoje, já passado) [D1] | o cronômetro começa às 08:40 e o decorrido conta desde então | `ApontamentosTests › Cronometro_ComecouAntes_DecorridoContaDesdeOInicio`; `cronometro.test › começou antes…`; `cronometro.test (shared) › inicioPelaHora` |
| F12-02 | idem | informa um horário no futuro | `400` em `errors.iniciadoEm`, nada começa | `ApontamentosTests › Cronometro_InicioForaDoLimite_400ENadaComeca(10)` |
| F12-03 | idem | informa um horário mais antigo que o limite [D1] | `400` em `errors.iniciadoEm`, com o limite na mensagem | `ApontamentosTests › Cronometro_InicioForaDoLimite_400ENadaComeca(-721)`; `cronometro.test › começou antes… mostra o erro da API na janela` |
| F12-04 | cronômetro rodando | abre "Parar cronômetro" | a duração vem preenchida com o decorrido e pode ser alterada [D2] | `cronometro.test › parar com outra duração…` |
| F12-05 | idem | para com outra duração (1 min a 24 h) | o apontamento sai com a duração informada, e não com o decorrido | `ApontamentosTests › Cronometro_PararComOutraDuracao_ApontaAEscolhida`; `cronometro.test › parar com outra duração…` |
| F12-06 | idem | para com duração 0 ou acima de 24 h | `400` em `errors.duracaoMinutos`, e o cronômetro continua rodando | `ApontamentosTests › Cronometro_PararComDuracaoInvalida_400EContinuaRodando`; `cronometro.test › … inválida não manda` |
| F12-07 | cronômetro iniciado ontem às 17:30 no fuso da empresa | para hoje | o apontamento sai na data do início (ontem), não na de hoje [D1] | `ApontamentosTests › Cronometro_PararNoDiaSeguinte_ApontaNoDiaDoInicio` |
| F12-08 | período do contrato fechado na data do início | para | `422 periodo-fechado`, e o cronômetro continua para a pessoa ajustar ou descartar (como hoje) | `ApontamentosTests › Cronometro_PeriodoDoDiaDoInicioFechado_422EContinuaRodando` |

### Iniciar junto com a tarefa

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F12-09 | pessoa com `tarefa.criar` e `apontamento.criar`, sem cronômetro | cria a tarefa com "Iniciar o cronômetro" marcado [D3] | a tarefa nasce e o cronômetro começa nela; a tarefa vai a FAZENDO (F11-19) | `nova.test › marcado, cria a tarefa e inicia nela`; a tarefa vai a FAZENDO pelo F11-19 (`SolicitacaoETarefasTests › IniciarCronometro_TarefaAFazer_PassaAFazendo`) |
| F12-10 | idem, sem `apontamento.criar` | abre a janela de nova tarefa | "Iniciar o cronômetro" não aparece | `nova.test › sem apontamento.criar, a opção não aparece` |
| F12-11 | pessoa com outro cronômetro rodando | abre a janela de nova tarefa | "Iniciar o cronômetro" aparece desmarcado e desabilitado, com "Já há um cronômetro em <tarefa>" | `nova.test › com outro cronômetro correndo, vem desabilitada e diz onde` |
| F12-12 | a tarefa foi criada, mas iniciar falhou (por exemplo, um cronômetro começou em outra aba) | — | a tarefa continua criada, e o aviso diz que o cronômetro não começou | `nova.test › se iniciar falhar, a tarefa fica criada…` |

### Lembrete no expediente

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F12-13 | pessoa ligou o lembrete neste navegador [D4], expediente da empresa aberto agora, nenhum cronômetro há 30 min | — | aparece "Nenhum cronômetro rodando. Começar agora?" (aviso no app e, se permitido, notificação do sistema) | `useLembreteCronometro.hook.test › montado, com o lembrete ligado e sem cronômetro, mostra o aviso`; `useLembreteCronometro.test › o lembrete é o aviso com "Minhas tarefas" e… a notificação`; `… ligado, lembra depois do intervalo…`; `conta.test › vem desligado e guarda o intervalo…` |
| F12-14 | idem | fora do expediente, em dia não útil ou em feriado do calendário | nada aparece | `expediente.test`; `useLembreteCronometro.test › fora do expediente, nada, e o cronômetro nem é consultado` |
| F12-15 | lembrete já mostrado | a pessoa continua sem cronômetro | o próximo só vem depois de mais um intervalo inteiro, não a cada consulta | `useLembreteCronometro.test › ligado, lembra… e não de novo antes de outro`; `… cronômetro rodando zera a contagem (F12-15)`; `… duas abas… lembram uma vez só` |
| F12-16 | lembrete desligado (padrão) | — | nada aparece | `useLembreteCronometro.test › desligado (padrão), nada`; `lembrete.test › desligado por padrão…`; `conta.test › voltar para Desligado…` |

### Cronômetro esquecido

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F12-17 | cronômetro rodando além do limite [D5] | a pessoa está com o app aberto | o indicador do cronômetro fica em destaque com "Ficou ligado? Parar e ajustar", e a janela de parar sugere como fim o limite do expediente | `CronometroEsquecidoTests (Domain) › AntesDoFimMais30_NaoEsta_ESugereAteOFim`, `TrintaMinutosDepoisDoFim_Esta`, `DezHorasAntesDoFim_Esta_ESemSugestaoNoFuturo`, `FimJaPassou_SugereDoInicioAteOFim`, `ComecouDepoisDoFim_SoAs10Horas_ESemSugestao`, `FimDeSemanaOuFeriado_SoAs10Horas`; `CronometroEsquecidoTests (Api) › Rodando10h_GetDizEsquecido…`; `cronometro.test › esquecido: o indicador pergunta se ficou ligado…` |
| F12-18 | idem, e o tick roda | — | a pessoa recebe um aviso no sino (e por e-mail, conforme a preferência), uma vez só por cronômetro [D5] | `CronometroEsquecidoTests (Api) › Rodando10h_GetDizEsquecido_ETickAvisaODonoUmaVez`; `… EmailDesligadoNaCategoria_SoOSino`; `… RecemIniciado_NaoEstaEsquecido_ETickNaoAvisa` |
| F12-19 | cronômetro de outra pessoa ou de outro tenant | o tick roda | o aviso vai só para o dono do cronômetro, no tenant dele | `CronometroEsquecidoTests (Api) › Rodando10h…` (membro e outra empresa: cada aviso só ao dono, no tenant dele) |

### Notificação do navegador

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F12-20 | pessoa da equipe em Minha conta > Neste navegador | liga "Notificações neste navegador" | o navegador pede a permissão; aceita, o switch fica ligado | `conta.test › ligar pede a permissão e fica ligado…`; `navegador.test › ligar pede a permissão…` |
| F12-21 | permissão negada no navegador | abre a configuração | o switch fica desligado, com o texto de como liberar nas configurações do navegador | `conta.test › bloqueada no navegador…`; `conta.test › negar na hora deixa desligada…`; `navegador.test › negada no navegador fica negada` |
| F12-22 | notificações ligadas, aba aberta (mesmo minimizada) | chega um aviso novo no sino | o sistema mostra a notificação com o título do aviso, uma vez por aviso [D6] | `casca.test › com a notificação do navegador ligada, aviso novo do sino vira notificação…`; `casca.test › com a aba escondida, o sino continua consultando…`; `navegador.test › mostra o aviso novo uma vez…` |
| F12-23 | idem | a pessoa clica na notificação | a aba vem para a frente e abre o item do aviso | `casca.test › … e o clique abre o item`; `navegador.test › clique foca a janela…` |
| F12-24 | duas abas do Acordu abertas | chega um aviso | só uma notificação aparece | `navegador.test › mostra o aviso novo uma vez…` (duas chamadas com o mesmo armazenamento simulam as abas; a corrida real, no mesmo instante, fica com o `tag` e não tem teste) |
| F12-25 | navegador sem suporte (ou contato no portal) | — | a opção não aparece; o portal não pede permissão | `conta.test › navegador sem suporte…`; `portal.test › o portal não pede permissão nem mostra notificação…` |

### App instalável

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F12-26 | Chrome ou Edge no computador ou no Android | abre o Acordu logado | o navegador oferece instalar; o menu da conta mostra "Instalar o Acordu" enquanto a instalação estiver disponível [D7] | `casca.test › o manifesto entra com a casca, e "Instalar o Acordu" aparece…`; `instalacao.test`; conferido no Chromium (`Page.getInstallabilityErrors` vazio) |
| F12-27 | app instalado | abre pelo ícone | abre em janela própria em `/hoje`, com o nome e o ícone do Acordu [D8] | `public/manifest.webmanifest` (janela própria, `start_url` `/`, ícones); conferido no Chromium (`Page.getAppManifest` sem erros) |
| F12-28 | app instalado e sem rede | abre | aparece a casca com "Sem conexão", sem dados de nenhum cliente em cache | `sw.test › navegação vai à rede; sem rede, mostra "Sem conexão"`; `… API e arquivos do app não passam pelo service worker`; conferido no Chromium offline |
| F12-29 | nova versão publicada | a pessoa abre o app | a versão nova vale no próximo carregamento, sem ficar presa na antiga | `sw.test › na instalação… assume na hora`; `… ao ativar, apaga as casas de versões velhas`; `_headers` com `no-cache` no `sw.js` e no manifesto |
| F12-30 | iPhone ou iPad (Safari) | abre a ajuda | a ajuda explica "Compartilhar > Adicionar à Tela de Início" | seção "Instalar o Acordu" da ajuda (`conteudo.ts`) |

## 3. Fora de escopo

- **Web Push** (notificação com o navegador fechado): precisa de service worker com inscrição, chaves VAPID em variável de ambiente, tabela de inscrições por pessoa e envio pelos serviços de push dos navegadores. Vai num marco próprio com ADR, que reaproveita o service worker do F12f.
- Mais de um cronômetro por pessoa, e pausar (RF-APT-02 continua: um ativo por pessoa).
- "Parar este e iniciar aquele" numa ação só.
- Lembrete configurado no servidor e seguindo a pessoa entre aparelhos (ver D4).
- Modo offline com dados: o service worker não guarda respostas da API.
- Portal instalável e notificação para o contato do cliente.
- App nas lojas (Play Store, App Store).

## 4. Decisões que dependem do Daniel

1. **D1 — Começar antes.**
   - (a) **Recomendado:** campo opcional "Começou às" ao iniciar, com hora de hoje ou de até 12 h atrás, nunca no futuro. O apontamento sai na data do início, no fuso da empresa (F12-07). O limite cobre esquecer de iniciar de manhã, e evita um cronômetro de dias.
   - (b) Sem limite além de "não no futuro", e a duração continua barrada em 24 h no apontamento.
   - (c) Não fazer: só o ajuste ao parar (D2).
2. **D2 — Ajustar ao parar.**
   - (a) **Recomendado:** a janela de parar ganha o campo Duração, preenchido com o decorrido e livre de 1 min a 24 h, como no apontamento manual. Corrige para mais (esqueceu de iniciar) e para menos (esqueceu de parar). A edição de apontamento já é auditada.
   - (b) Só para menos (duração ≤ decorrido).
   - (c) Campo "Terminou às" em vez de duração.
3. **D3 — Iniciar junto com a tarefa.**
   - (a) **Recomendado:** marcador "Iniciar o cronômetro" na janela de nova tarefa. O front cria a tarefa e depois inicia, com as rotas de hoje. Se iniciar falhar, a tarefa fica criada e o aviso diz isso (F12-12). Não muda a API.
   - (b) Campo `iniciarCronometro` no `POST /tarefas`, numa transação. É atômico, mas mistura dois casos de uso e duas permissões numa rota.
4. **D4 — Lembrete no expediente.**
   - (a) **Recomendado:** ligado por pessoa e por navegador (no próprio navegador, sem migration), desligado por padrão. Usa o expediente e os feriados do calendário da empresa (`GET /configuracoes/calendario`), avisa depois de 30 min sem cronômetro (15, 30 ou 60 min, à escolha) e só com o app aberto. Combina com a permissão de notificação, que também é por navegador.
   - (b) Preferência guardada no servidor, seguindo a pessoa entre aparelhos. Precisa de migration e de rota.
   - (c) Só um lembrete no início do expediente.
5. **D5 — Cronômetro esquecido.**
   - (a) **Recomendado:** "esquecido" é passar 30 min do fim do expediente do dia em que começou, ou 10 h rodando, o que vier primeiro.
     - No app: o indicador fica em destaque, e a janela de parar sugere a duração até o fim do expediente (F12-17).
     - No servidor: o tick de 15 min manda um aviso no sino e por e-mail na categoria nova `CRONOMETRO` (e-mail ligado por padrão, desligável em Preferências), uma vez por cronômetro. Precisa de uma coluna `AvisadoEsquecidoEm` no cronômetro (migration aditiva). *Na implementação (F12e, registro de 09/11): sem migration, pelos marcadores de aviso que o SLA já usa.*
   - (b) Só no app, sem aviso do servidor: não alcança quem fechou a aba.
   - (c) Parar sozinho no fim do expediente: aponta horas que a pessoa não confirmou.
6. **D6 — Notificação do navegador.**
   - (a) **Recomendado:** API de notificações do navegador com a aba aberta (inclusive minimizada), ligada por pessoa em Configurações > Conta. Mostra:
     - os avisos novos do sino, achados na consulta de 60 s que já existe;
     - o lembrete (D4);
     - o cronômetro esquecido (D5).

     Uma aba só notifica (`BroadcastChannel` mais uma trava curta; na implementação, marca no `localStorage` mais o `tag` da notificação, registro de 09/11), e o clique abre o item. Não precisa de servidor nem de chave. O Web Push fica para depois, com ADR.
   - (b) Web Push já neste marco: alcança a aba fechada, mas pede ADR, chaves, tabela e envio, e dobra o marco.
7. **D7 — Instalação.**
   - (a) **Recomendado:** só a equipe. O conjunto tem quatro peças:
     - manifesto com `start_url` `/hoje` e janela própria;
     - service worker mínimo, que guarda só os arquivos da casca e nunca as respostas da API, servido sem cache (F12-29);
     - página "Sem conexão";
     - "Instalar o Acordu" no menu da conta quando o navegador oferecer.

     O mesmo service worker serve depois ao Web Push. A CSP atual já cobre (`default-src 'self'`); o `_headers` ganha a regra de cache do `sw.js`.
   - (b) Só o manifesto, sem service worker: instala no Chrome e no Edge, mas sem a página offline e sem base para o Web Push.
8. **D8 — Ícone.**
   - (a) **Recomendado:** derivar do `favicon.svg` atual (o "a" em itálico dourado sobre o couro, com a costura tracejada), com a letra convertida em contorno para não depender de fonte. Gera PNG de 192 e 512 px, a versão "maskable" com margem segura e o ícone da Apple de 180 px. É provisório, e trocar depois é só substituir os arquivos.
   - (b) O Daniel fornece um logotipo definitivo antes do F12f.

## 5. Impacto nos documentos

- **ERS:**
  - RF-APT-02 ganha "iniciar com horário anterior (até 12 h) e ajustar a duração ao parar; iniciar ao criar a tarefa; lembrete opcional no expediente; aviso de cronômetro esquecido".
  - **RF-NOT-05 (novo):** "Notificação do sistema operacional pelo navegador, opcional por pessoa, com o app aberto".
  - **RNF-UX-01 (novo):** "App instalável (PWA) para a equipe, sem dados em cache".
  - Já atualizados no PR da spec.
- **Contrato da API:**
  - `POST /cronometro/inicio` com `iniciadoEm` opcional;
  - `POST /cronometro/parada` com `duracaoMinutos` opcional e a data do apontamento pelo início;
  - `GET /cronometro` com `esquecido` (bool) e `sugestaoMinutos`;
  - a categoria `CRONOMETRO` em §12b.
- **Modelo de domínio:** `Cronometro.Iniciar` aceita um início anterior (validado pela Application, que tem o relógio), a Application troca a duração do `Parar` pela escolhida, e entra `MarcarAvisoEsquecido` (*no F12e o domínio não mudou: a regra é o serviço `CronometroEsquecido`, e a marca é o marcador de aviso*).
- **Segurança:** a permissão de notificação e o lembrete ficam no navegador; o service worker não guarda resposta da API (isolamento entre tenants e clientes no aparelho compartilhado).
- **Deploy:** o tick ganha o passo `cronometros-esquecidos` (§5); `_headers` com `Cache-Control: no-cache` para `sw.js` e `manifest.webmanifest`.
- **Convenções:** notificação do navegador só com a aba aberta, uma por aviso e uma aba só; o service worker não guarda resposta da API.
- **ADR novo:** nenhum neste marco. O Web Push terá o dele.
- **Ajuda e Novidades:** apontamento e cronômetro, conta (notificações) e uma seção "Instalar o Acordu".

## 6. Divisão em PRs

Cada PR contra a `main`, sem empilhar, com testes e CI verde.

| PR | Entrega | Cenários |
|---|---|---|
| F12a | Começar antes e ajustar ao parar (API, domínio, janela) | F12-01 a F12-08 |
| F12b | Iniciar junto com a tarefa | F12-09 a F12-12 |
| F12c | Notificação do navegador (configuração, avisos do sino, uma aba só) | F12-20 a F12-25 |
| F12d | Lembrete no expediente | F12-13 a F12-16 |
| F12e | Cronômetro esquecido (app, tick, categoria `CRONOMETRO`; sem migration, registro de 09/11) | F12-17 a F12-19 |
| F12f | App instalável e ícone | F12-26 a F12-30 |

O F12a, o F12b e o F12f são independentes. O F12d e o F12e usam a notificação do F12c quando ela estiver ligada, mas funcionam sem ela (aviso no app).

## 7. Registro

- 09/11 — rascunho a partir do pedido do Daniel de 06/11 (cronômetro, pop-up do Windows com a aba minimizada, "baixar o sistema como o YouTube permite"; "pode incluir").
- 09/11 — o Daniel aprovou todas as recomendações (D1 a D8) e pediu o PR da spec.
- 09/11 — F12a: o domínio não mudou (`Cronometro.Iniciar` já recebia o instante; a Application valida o limite com o relógio). "Começou às" é hora no fuso da empresa (convenções: instantes no fuso do tenant), hoje, ou ontem se ainda não chegou. A Duração vem congelada ao abrir a janela e só vai à API se a pessoa mudou o texto.
- 09/11 — F12b: só front. "Iniciar o cronômetro" vem desmarcado (opção da pessoa), cria a tarefa e depois chama `POST /cronometro/inicio`; a falha do segundo passo não desfaz o primeiro e vira aviso.
- 09/11 — F12c: o "switch" é o marcador "Notificações neste navegador" em Minha conta > Neste navegador. Uma aba só: em vez de `BroadcastChannel`, a marca de "já mostrada" no `localStorage` (que as abas compartilham) mais o `tag` da notificação, que faz o sistema trocar em vez de empilhar. Só avisos nascidos depois de ligar e há menos de 10 min, para reabrir o app não despejar avisos antigos. No Android, o Chrome só mostra notificação pelo service worker: lá, até o F12f, fica o sino. A escolha vale por pessoa e por empresa no navegador (chave com tenant e usuário), para quem entra depois no mesmo computador não herdar. Com ela ligada, o sino consulta também com a aba escondida (sem isso, o TanStack Query para e o pop-up só sairia com o app à vista). O pop-up sai também com o app visível: é o mesmo aviso do sino, e quem não quer desliga. 2 min de folga para o relógio da máquina diferente do do servidor.
- 09/11 — F12d: o lembrete fica em Minha conta > Neste navegador ("Lembrete do cronômetro": Desligado, 15, 30 ou 60 min), guardado por empresa e pessoa como a notificação. A contagem parte do mais recente entre o início do expediente, a abertura do app, a última vez com cronômetro e o último lembrete. O aviso leva a "Minhas tarefas" (`/tarefas?responsavel=eu`). A conferência roda a cada minuto (o navegador também limita os timers da aba escondida a um por minuto) e lê o calendário primeiro: fora do expediente, nem consulta o cronômetro. O último lembrete fica no navegador, compartilhado entre as abas, e ligar (ou mudar o intervalo) também conta como ponto de partida. Sem a API de notificação, o lembrete continua como aviso no app.
- 09/11 — F12e: sem migration. Em vez da coluna `AvisadoEsquecidoEm`, o "uma vez por cronômetro" usa a tabela de marcadores de aviso que os alertas de SLA já usam (chave `cronometro-esquecido` no id do cronômetro); o domínio não mudou. A regra fica em `CronometroEsquecido` (serviço de domínio, ao lado da `CalculadoraSla`, com a mesma conversão de hora local que trata o horário de verão): sem expediente no dia do início, ou começado depois do fim, vale só as 10 h. O app lê `esquecido` do `GET /cronometro` (relido a cada 5 min) e a janela de parar já vem com a duração até o fim do expediente, que vai à API mesmo sem a pessoa mexer. A sugestão só existe quando esse fim já passou: esquecido pelas 10 h antes do fim, não há sugestão (nunca se aponta tempo que não correu). O `POST /cronometro/inicio` devolve a mesma leitura do GET. A notificação do sistema vem pelo aviso do sino (F12c), sem uma segunda pelo app. O tick roda das 7h às 19h, de segunda a sábado: fora disso, o aviso sai no primeiro tick seguinte.
- 09/11 — F12f: o `start_url` é `/` (a tela Hoje fica na raiz, não em `/hoje`). O manifesto entra pela casca da equipe (o portal não se oferece para instalar); o ícone da Apple e a cor do tema ficam no `index.html`. Os ícones saem do desenho do favicon com a fonte do app (Fraunces) embutida, por `scripts/gerar-icones.mjs`. O service worker também mostra a notificação do sistema onde a página não pode (Android), o que fecha a lacuna registrada no F12c. Sem service worker em desenvolvimento.
- A spec é viva: mudou o comportamento, a spec muda no mesmo PR.
