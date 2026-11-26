# Acordu — Visão, Estratégia de Produto e Monetização

| | |
|---|---|
| **Versão** | 0.2 (documento vivo — revisar conforme validar) |
| **Data** | 04/11/2026 |
| **Empresa** | Nebrys |
| **Natureza** | Visão e estratégia. Hipóteses a validar com usuários, não decisões cravadas. |

> **Nome:** **Acordu** — de *acordo* (PT), *acuerdo* (ES) e *accord* (EN): o acordo com o cliente (contrato, SLA, entregas) que o produto torna visível. O "u" final torna o nome inventado (defensável como marca). Domínios `acordu.app.br` e `acordu.com.br` livres em 30/09/2026. Busca exata no INPI (30/09/2026): nenhum resultado para "Acordu" nem "Acordo". **Pendente:** busca por radical e registrar os domínios.

---

## 1. Origem e fronteira com o ASMTasks

O Acordu nasce da **ideia** do ASMTasks (tarefas, projetos e atendimentos), construído como estágio supervisionado para uso interno da empregadora. O ASMTasks ficou moldado ao sistema antigo e às particularidades de **uma** software house.

Regras de fronteira (inegociáveis):
- **Nada é reaproveitado** do ASMTasks: código, schema, scripts, documentação, telas, dados ou nomes. O Acordu é escrito do zero, a partir deste documento.
- **Público diferente:** o Acordu mira prestadores de serviço de **qualquer** área, não software houses — mercado distinto do da empregadora.
- Antes de comercializar: revisar o contrato de trabalho e, se houver dúvida, consultar um advogado.

---

## 2. Diagnóstico de mercado

O mercado está partido em três categorias, e nenhuma serve bem o pequeno prestador de serviço:

| Categoria | Exemplos | Faz bem | Deixa de lado |
|---|---|---|---|
| **Help desk** | Zendesk, Freshdesk, Movidesk, TomTicket, Milvus, Octadesk | Chamados, canais, SLA | Execução do trabalho, horas, contrato, faturamento |
| **Gestão de tarefas/projetos** | ClickUp, Jira, Trello | Tarefas, quadros, projetos | Triagem de chamados, SLA, cliente externo, faturamento |
| **PSA** | Autotask, ConnectWise, HaloPSA | Tudo integrado (chamado → horas → contrato → fatura) | Pesado, caro, implantação longa, **feito só para TI** |

**Lacuna:** um "PSA leve" para pequenos prestadores de serviço **não-TI** (agências, contabilidade, consultorias, manutenção, escritórios técnicos, freelancers com equipe), que hoje costuram 3–4 ferramentas.

**Dores recorrentes levantadas:**
1. Atendimento e execução desconectados — o pedido chega num lugar, o trabalho acontece em outro.
2. Horas e contrato invisíveis para o cliente — fonte de atrito na cobrança (banco de horas, mensalidade).
3. Portal do cliente fraco — status na linguagem do atendente, sem SLA nem horas visíveis.
4. WhatsApp é o canal real no Brasil, mas a integração oficial costuma ficar nos planos caros.
5. Preço por agente + add-ons (IA, canais) — o custo real fica várias vezes acima do anunciado.
6. Trabalho recorrente/agendado (obrigações mensais, manutenções preventivas) tratado como gambiarra — ferramentas só reagem.

Referências de preço (ordem de grandeza, 2026): Zendesk a partir de US$19/agente (real bem acima com add-ons); Freshdesk a partir de US$15–19/agente; Movidesk a partir de ~R$700/mês; Octadesk a partir de ~R$2.499/mês; Milvus R$95–205/licença.

---

## 3. Público-alvo

**Primário:** pequenos prestadores de serviço B2B (2–30 pessoas) que atendem uma carteira de clientes recorrentes, com contrato (mensal ou banco de horas) e demandas que chegam por WhatsApp/e-mail.

O critério não é o setor, é o jeito de trabalhar: carteira de clientes recorrentes, contrato mensal, banco de horas ou trabalho por hora, pedidos que chegam por WhatsApp/e-mail, trabalho periódico e a necessidade de prestar contas ao cliente.

### 3.1 Verticais

**Atendidas pelo v1** (o modelo atual cobre o fluxo do dia a dia):

| Vertical | Como usa o Acordu |
|---|---|
| Agência de marketing, design e social media | Pedidos de ajuste como solicitação; campanhas como projeto; contrato mensal com franquia; aprovação de entrega no portal. |
| Escritório de contabilidade | Obrigações mensais como tarefa recorrente; dúvidas de clientes como solicitação com SLA; contrato mensal. |
| Consultoria (jurídica, RH, gestão, TI) | Banco de horas ou mensal; horas apontadas por cliente; extrato para prestar contas. |
| Empresa de manutenção (predial, equipamentos, climatização) | Chamados com SLA; preventivas como tarefa recorrente; contrato de manutenção mensal. |
| Suporte de TI terceirizado de pequeno porte | Chamados com SLA em horas úteis; banco de horas. |
| Freelancer com equipe (dev, redação, tradução) | O mesmo fluxo em escala menor; banco de horas ou avulso. |

**Atendidas com ajustes (v2)** (a ideia encaixa, mas o contrato ou a entrega têm forma própria):

| Vertical | O que já funciona | O que falta |
|---|---|---|
| **Escritório de arquitetura** | Projeto com tarefas por fase; pedidos de revisão como solicitação; portal com andamento e aprovação; horas para medir a rentabilidade do projeto. | Contrato **por projeto com preço fechado e pagamento por etapa**; **limite de revisões** por etapa; **entregas versionadas** (pranchas R01, R02) com aprovação formal e arquivos grandes (PDF/DWG > 20 MB); **cronograma de fases** com datas e marcos externos (aprovação na prefeitura); **acompanhamento de obra** (visitas e registros). SLA em horas úteis pesa menos que o prazo da etapa. |
| Escritório de engenharia (projetos) | Igual à arquitetura. | As mesmas lacunas da arquitetura, mais registro de ART por projeto. |
| Produtora de vídeo / estúdio criativo | Projetos, pedidos de alteração, aprovação no portal. | Contrato por entrega com rodadas de alteração limitadas; entregas grandes e versionadas (vídeo). |

Os ajustes de arquitetura, engenharia e estúdios se sobrepõem: **contrato por projeto com etapas, limite de revisões e entregas versionadas** resolvem os três. São o candidato natural a pacote da v2, depois de validar com 2–3 escritórios (perguntas: como cobram, quantas revisões incluem, onde mais perdem tempo com o cliente).

**Fora do alvo:** contact centers de alto volume (B2C), ITSM corporativo com processos ITIL, software houses (distância do mercado da empregadora, §1) e negócios sem cliente recorrente nem contrato (varejo, serviço avulso para pessoa física).

---

## 4. Posicionamento

> **"Todo pedido do cliente vira trabalho rastreado — e o cliente vê o que foi feito e quanto do contrato foi consumido."**

- **Herói:** o fluxo contínuo **solicitação → tarefa/projeto → horas → contrato → prestação de contas ao cliente**, numa ferramenta só.
- **Muralha:** transparência para o cliente (portal) + simplicidade (implanta em uma tarde, não em semanas).
- **Assinatura técnica:** projeção determinística e explicável aplicada ao contrato.

---

## 5. Conceitos centrais do domínio (visão, não modelo final)

| Conceito | O que é |
|---|---|
| **Tenant** | O prestador de serviço (a empresa que assina o Acordu). |
| **Cliente** | Organização atendida pelo prestador. Pertence ao tenant. |
| **Contato** | Pessoa do cliente que solicita/acompanha. Pode ter ou não login no portal. |
| **Solicitação** | Pedido do cliente (chega pelo portal, e-mail ou WhatsApp). Tem SLA, status e histórico. |
| **Tarefa** | Unidade de execução. Pode nascer de uma solicitação, de um projeto ou de uma recorrência. |
| **Projeto** | Agrupamento de tarefas com prazo/escopo (ex.: "implantação", "campanha de lançamento"). |
| **Apontamento** | Registro de tempo gasto numa tarefa (faturável ou não). |
| **Contrato** | Acordo com o cliente: mensal fixo, banco de horas ou avulso; define SLA e o que é faturável. |
| **Recorrência de tarefa** | Trabalho agendado que se repete (ex.: "fechamento mensal do cliente X", "preventiva trimestral"). |

Modelagem detalhada em `modelo-de-dominio-acordu.md`.

---

## 6. Portal do cliente

**Decisão: sim, o cliente visualiza o status do serviço solicitado** — é um diferencial central, não acessório.

O que o contato do cliente vê/faz:
- Abrir solicitações (formulário estruturado, anexos) e acompanhar as suas — ou todas da sua organização, conforme permissão.
- **Status na linguagem do cliente**, não do atendente. Os status internos são mapeados para poucas visões externas: *Recebida → Em andamento → Aguardando você → Concluída*.
- Responsável pela solicitação e **relógio do SLA** contra o prazo contratado.
- **Extrato do contrato:** horas consumidas × contratadas no período, com o detalhe por tarefa.
- Aprovar/reprovar entregas; reabrir solicitação.

Regras de visibilidade:
- Cada comentário/anexo/tarefa tem visibilidade **interna** ou **externa**. Padrão: interno (seguro por omissão).
- Apontamentos aparecem ao cliente só quando marcados como faturáveis/visíveis.

Implicação arquitetural (ADR 0002):
- O contato do cliente é um `Usuario` com `Vinculo` no tenant do prestador, com um papel **Cliente** e **escopo por `ClienteId`**.
- Isolamento em duas camadas: *global query filter* por `TenantId` (ADR 0001) **+** filtro por `ClienteId` para usuários do portal. Teste obrigatório: contato do cliente A nunca vê dados do cliente B do mesmo tenant.
- Acesso sem senha opcional: link mágico por solicitação (para contatos que não querem conta).

---

## 7. Diferenciais (features-herói)

1. **Fluxo contínuo** atendimento → execução → horas → contrato, sem trocar de ferramenta.
2. **Portal transparente** (§6).
3. **Tarefas recorrentes nativas** — o prestador programa o trabalho periódico e o sistema gera as tarefas.
4. **Projeção do contrato** (assinatura técnica): *"no ritmo atual, o cliente X esgota o banco de horas dia 18"*; *"este mês o cliente Y consumiu 30% acima da média"*. Determinística, com a base exposta.
5. **Preço previsível:** não cobrar pelos contatos do cliente (portal ilimitado); cobrança por equipe interna ou faixa fixa.
6. **WhatsApp** como canal de entrada (fase posterior — ver §8).
7. **Base de conhecimento que se escreve sozinha:** o que foi resolvido vira conhecimento reutilizável, em duas camadas:
   - **Determinística (sem IA):** ao concluir, a solicitação registra uma **solução** estruturada (problema → causa → solução). Ao abrir uma nova, o sistema sugere **solicitações semelhantes já resolvidas** (busca full-text + similaridade de trigramas no Postgres). Um clique transforma uma solução em artigo.
   - **Assistida por IA:** gera **rascunho** de artigo a partir do histórico da solicitação, removendo dados do cliente. **Nunca publica sozinha** — um humano revisa e aprova.
   - Artigos **internos** (equipe) ou **externos** (publicados no portal, para o cliente se resolver antes de abrir solicitação — deflexão de chamado).
   - Sempre **por tenant**: o conhecimento de um prestador nunca alimenta outro, nem treina modelo.

---

## 8. Escopo por fase

**v1 (núcleo):**
- Autenticação, multi-tenant, RBAC (ADR 0001) + papel Cliente com escopo.
- Clientes, contatos, contratos (mensal / banco de horas / avulso) com SLA.
- Solicitações (portal + criação interna), tarefas, projetos, apontamento de horas.
- Tarefas recorrentes.
- Portal do cliente (status, SLA, extrato de horas).
- Notificações por e-mail (Resend).
- Entrada por e-mail: o e-mail ao endereço da empresa vira solicitação, a resposta aos avisos entra na conversa e os avisos seguem pelo canal de origem (F15, trazido da v2 em 25/11/2026; WhatsApp no #184).
- Dashboard interno: fila, SLA em risco, horas por cliente/contrato.
- Campo de **solução** ao concluir solicitação + sugestão de **solicitações semelhantes resolvidas** (sem IA).

**v2:**
- **Ajustes para verticais de projeto** (arquitetura, engenharia, estúdios — §3.1): contrato por projeto com preço fechado e etapas; limite de revisões por etapa; entregas versionadas com aprovação formal e arquivos maiores; cronograma de fases com marcos externos; registro de visitas de obra.
- **Base de conhecimento:** artigos internos/externos, publicação no portal, "transformar solução em artigo".
- Projeção do contrato (§7.4).
- Relatório mensal de prestação de contas (PDF) por cliente.
- **Rascunho da NFS-e** (passo intermediário da §8.1): ao fechar o mês de um contrato, montar os dados da nota (tomador, descrição, valor, código do serviço) para o prestador copiar no emissor nacional ou mandar ao contador. Sem emissão nem certificado.
- Integração WhatsApp (API oficial via provedor).
- **Colaboração entre autônomos e equipes informais** (estudantes, freelancers parceiros):
  - "modo equipe" escolhido no cadastro, que esconde clientes, contratos e SLA e troca "empresa" por "espaço";
  - projeto sem cliente;
  - convidado restrito a um projeto, sem ver o resto do espaço (mexe no modelo de segurança: pede ADR).
  - Hoje já funciona criando um espaço e convidando, com as fricções de vocabulário e do projeto que exige cliente.
- **Integrações com ferramentas de colaboração que o outro lado já usa**, para quem colabora sem usar o Acordu:
  - **GitHub:** vincular issue ou PR a tarefa e solicitação, com o status do PR na tarefa via webhook;
  - **Notion:** vincular página e espelhar tarefas num database;
  - **Slack, Discord e Teams:** avisos do sino num canal;
  - **Google Agenda e Outlook:** o v1 já publica a agenda em ICS (somente leitura); no v2, criar e atualizar eventos pela API, nos dois sentidos.
  - **Princípio:** o Acordu é a fonte da verdade do trabalho e das horas, e as integrações levam e trazem sinais sem exigir conta do outro lado.

**v3+:**
- Faturamento/cobrança e **emissão de NFS-e** (§8.1) — ou integração com sistemas financeiros.
- IA (via API externa), nunca no caminho crítico: rascunho automático de artigo a partir de solicitações resolvidas (revisão humana obrigatória), triagem e resumo.

### 8.1 Faturamento e NFS-e (registrado em 04/11/2026)

**Por quê:** fecha o fluxo que o produto já cobre (contrato → horas → fechamento do mês → fatura → nota) e leva a nota ao portal do cliente, no mesmo espírito de transparência. Para quem presta serviço a nota é a NFS-e (NF-e é de mercadoria).

**Cenário (conferido em 04/11/2026; reconferir antes da spec):**
- Desde 01/11/2026, ME e EPP do Simples Nacional que prestam serviço sujeito a ISS emitem pela **NFS-e de padrão nacional** (Resolução CGSN 191/2026, que adiou de 01/09/2026). O MEI já usa o emissor nacional.
- O ambiente nacional tem API para sistemas, além do portal web e do aplicativo: para o público do Acordu (quase todo Simples ou MEI), é uma integração só, e não uma por município.
- A reforma tributária está acrescentando IBS/CBS à NFS-e por notas técnicas do padrão nacional: é a parte que ainda muda.

**Caminho:**
1. **v2 — rascunho da nota** (acima, em v2): dados prontos para copiar, sem emitir.
2. **v3 — emissão por gateway** (Focus NFe, NFE.io, PlugNotas/Tecnospeed ou similar): o gateway guarda o certificado digital, assina, acompanha layout e reforma e cobre municípios fora do padrão nacional. Custo por nota ou mensal, repassável no plano. Integração atrás de uma interface própria (`IEmissorNfse`), por HTTP, sem SDK (ADR 0010); a escolha do gateway vira ADR.
3. **Depois, com volume e CNPJ próprio — direto na API nacional:** sem custo por nota, mas o Acordu passa a guardar o certificado A1 de cada prestador e assinar o XML; responsabilidade de segurança alta e cada nota técnica vira trabalho próprio. Decidir em ADR.

**Pré-requisitos no produto:** valores monetários (`Dinheiro`: mensalidade, valor-hora) e fatura por contrato e mês; dados fiscais do prestador (regime: MEI, Simples, Presumido; código do serviço pela LC 116 / código nacional; alíquota de ISS; retenções); tomador com CNPJ/CPF e endereço (RF-CLI-01 e RF-CLI-08 já cobrem); nota visível no portal.

**Antes da spec:** revisão por contador das regras fiscais (regimes, retenções, campos de IBS/CBS) — não é algo para decidir só pela documentação.

---

## 9. Monetização (hipóteses)

- **Freemium enxuto** ou **trial + plano pago** — decidir após validação.
- Cobrança **por usuário interno** (atendente/executor) ou **por faixa** (até N usuários), com **portal de clientes ilimitado** — o contraponto direto ao "preço por agente + add-ons".
- Canal WhatsApp como add-on transparente (custo repassado da Meta/provedor), nunca surpresa.
- Emissão de NFS-e (§8.1) com o custo do gateway embutido no plano ou como add-on transparente por faixa de notas.
- Referência de faixa a validar: abaixo de Movidesk (R$700/mês) para equipes pequenas.

---

## 10. Engenharia e infraestrutura

- **Stack:** .NET 10, EF Core + Npgsql, PostgreSQL 17, React + Vite (TS), GitHub Actions; Cloud Run + Neon + Cloudflare (Pages, Worker, R2) nos planos gratuitos.
- **Convenções:** estrutura de repositório, DDD pragmático em 4 camadas, `convencoes-codigo`, Conventional Commits, ADRs, ERS.
- **ADRs:** 0001 (decisões de engenharia base), 0002 (portal/escopo por cliente), 0003 (SLA em horas úteis), 0004 (tempo em minutos e consumo como read model), 0005 (hospedagem serverless — histórico), 0006 (VM Oracle compartilhada — revogado), 0007 (transporte da sessão), 0008 (trilha de auditoria), 0009 (tempo real por SSE), 0010 (hospedagem serverless com o Acordu como único produto e plano de escala).
- **Infra:** própria do Acordu, sem nada compartilhado: projeto `acordu-prod` no Google, projeto Neon `acordu`, conta Cloudflare da Nebrys (ADR 0010). Ao abrir CNPJ, decidir entre continuar no gerenciado pago ou ir para uma VM. Repositório **próprio**.
- **Assinatura técnica:** o motor de projeção determinística e explicável.

---

## 11. Modo de construção

**O Acordu é implementado pelo Claude**, com o Daniel como revisor e dono das decisões:
- Daniel define/aprova visão, ERS, modelo e ADRs; Claude implementa por marcos, com testes.
- Toda entrega passa por revisão (PR) do Daniel antes do merge.
- Consequência de agenda: o Acordu consome sobretudo tempo de **revisão e decisão**, não de digitação.

---

## 12. Riscos

- **Fronteira com a empregadora** (§1): manter rigorosamente; nada do ASMTasks entra.
- **Escopo:** PSA tende a crescer infinito. Guardar o v1 no fluxo núcleo; faturamento e WhatsApp ficam fora.
- **Mercado amplo demais:** "qualquer prestador" pode virar "ninguém". A validação deve escolher 1–2 verticais de entrada.
- **Vários produtos, um dev:** revisar o cronograma do Acordu junto com os demais a cada marco.
- **Nome:** "acordo" também evoca renegociação de dívida e acordo judicial no Brasil (ex.: Acordo Certo); busca exata no INPI limpa; busca por radical pendente.

---

## 13. Perguntas a validar

- Prestadores pequenos reconhecem "prestar contas das horas/contrato ao cliente" como dor própria?
- O portal do cliente é motivo de compra ou só "bom de ter"?
- Qual vertical de entrada responde melhor (agência? contabilidade? manutenção?)
- Pagariam por equipe interna com portal ilimitado? Em qual faixa?
- WhatsApp é requisito de compra desde o início ou pode esperar a v2?
- Emitir a NFS-e pelo Acordu é motivo de compra, ou o prestador prefere deixar a nota com o contador? O rascunho (v2) basta para quantos?

---

## 14. Registro de decisões (vivo)

| Data | Decisão / hipótese | Status |
|---|---|---|
| 30/09/2026 | Refazer a ideia do ASMTasks como produto novo, do zero, sem reaproveitar nada | Decidido |
| 30/09/2026 | Nome: **Acordu** (substitui o nome de trabalho Pensum; domínios livres; busca exata no INPI sem resultados) | Decidido |
| 30/09/2026 | Público: pequenos prestadores de serviço de qualquer área, **não** software houses | Hipótese |
| 30/09/2026 | Posicionamento: "PSA leve" — atendimento → execução → horas → contrato, com transparência ao cliente | Hipótese |
| 30/09/2026 | **Portal do cliente com status do serviço** é diferencial central (status externos, SLA, extrato de horas) | Decidido |
| 30/09/2026 | Contato do cliente = `Usuario` + `Vinculo` com papel Cliente e escopo por `ClienteId` (ADR 0002) | Decidido |
| 30/09/2026 | Stack e decisões de engenharia base registradas no ADR 0001 | Decidido |
| 30/09/2026 | Implementação feita pelo Claude; Daniel revisa e decide | Decidido |
| 30/09/2026 | **Base de conhecimento gerada a partir das solicitações resolvidas**: v1 solução estruturada + semelhantes (sem IA); v2 artigos e portal; v3 rascunho por IA com aprovação humana; sempre isolada por tenant | Hipótese |
| 02/10/2026 | Estratégia e cronograma ficam **no repositório** (`docs/`), privado; revisar antes de qualquer abertura pública | Decidido |
| 05/10/2026 | Conta Oracle Cloud inacessível: hospedagem serverless gratuita (Cloud Run + Neon + Cloudflare Pages), projeto e banco próprios por produto, plano de escala até VM no Brasil (ADR 0005) | Revogado em 06/10 |
| 06/10/2026 | Conta Oracle recuperada: volta à VM Ampere compartilhada com outros produtos; níveis 1 (Magalu Cloud SP) e 2; serverless como plano B (ADR 0006) | Revogado em 26/10 |
| 10/10/2026 | Empresa: **Nebrys** (`nebrys.com.br` livre em 10/10/2026; INPI a verificar) | Decidido |
| 15/10/2026 | Colaboração de autônomos/estudantes ("modo equipe", projeto sem cliente, convidado por projeto) e integrações com GitHub, Notion, Slack/Discord/Teams e agendas: **v2**, fora do v1 para não diluir o foco em prestadores | Hipótese |
| 16/10/2026 | Verticais: **v1** atende agência, contabilidade, consultoria, manutenção, TI terceirizada e freelancer com equipe; **arquitetura, engenharia e estúdios** precisam de ajustes (contrato por projeto com etapas, limite de revisões, entregas versionadas) e ficam para a **v2**, após validação | Hipótese |
| 26/10/2026 | Conta Oracle desativada de vez (nova só com CNPJ) e **o Acordu passa a ser o único produto da Nebrys**: serverless gratuito (Cloud Run + Neon + Cloudflare Pages, Worker e R2), API em `api.acordu.app.br`, polling no lugar do SSE no nível 0, tick a cada 15 min no expediente (ADR 0010) | Decidido |
| 04/11/2026 | **Faturamento e NFS-e** (§8.1): v2 rascunho da nota; v3 emissão por gateway atrás de `IEmissorNfse`; API nacional direta só com volume e CNPJ próprio; regras fiscais revisadas por contador antes da spec | Hipótese |
| 25/11/2026 | **Entrada por e-mail** sai da v2 e entra no v1 (marco F15, issue #183): endereço por empresa, resposta na conversa, triagem dos não identificados e avisos pelo canal de origem; o mecanismo de canal serve ao WhatsApp (#184) | Decidido (G1) |
