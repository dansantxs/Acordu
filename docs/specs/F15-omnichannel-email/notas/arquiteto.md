# Notas do arquiteto — F15 (etapa 3)

Premissas do design (25/11/2026). O que precisa de conferência fora do código está marcado "conferir" e tem PR dono no design §3.

## F15-79 (resolvido)

- A dúvida (`404` × `403` para o contato na triagem) foi decidida pelo Daniel: `403`, como as demais rotas da equipe (ADR 0002, item 4). Sem pendência; o cenário está no F15k (PR das rotas).

## Fatos de provedor a conferir (não verificados nesta etapa, sem acesso aos painéis)

- Cloudflare Email Routing: subdomínio (`entrada.acordu.app.br`) com regra *catch-all* própria; Worker chamado uma vez por destinatário (o F15-21 depende disso: e-mail para A e B no mesmo domínio); erro lançado no Email Worker vira falha temporária ao servidor de origem (reenvio); limite de 25 MiB; carimbo `Authentication-Results`/`ARC-Authentication-Results` visível no `message.headers` do Worker. Dono: F15d (provisionamento e e-mail real). Se o carimbo não vier, D11 (b) antes do F15f.
- Cloudflare: não há nota de spam nem antivírus expostos ao Email Worker no plano gratuito (premissa; registrada como risco no D11 e no ADR 0016).
- Resend: aceita `headers` personalizados no `POST /emails` (`Auto-Submitted`, `In-Reply-To`, `References`). Um `Message-ID` próprio não é necessário (D3 reconhece a resposta pelo destinatário). Dono: F15c.
- Gmail: agrupar a conversa com o assunto `Re: {assunto} (#N)` (D4). Dono: F15f.
- Inbound do Resend (D1 b): formato e cota do plano gratuito não conferidos; por isso é só alternativa.

## Premissas de projeto

- Os Workers ficam em `infra/cloudflare/` (o handoff citava `workers/`, que não existe); o novo é `infra/cloudflare/email-entrada/`.
- MimeKit (MIT) é biblioteca de protocolo, não SDK de provedor (ADR 0010 §10, como o `AWSSDK.S3`).
- Remetente = cabeçalho `From` (o que o DMARC alinha); envelope só para reconhecer devolução.
- O token da conversa e o código da empresa ficam em claro: são roteamento, não credencial (a entrada exige remetente contato ativo do cliente e autenticidade). A convenção de "só o hash" continua para segredos de uso único.
- Uma conversa por solicitação (não por contato): o colega em cópia responde ao mesmo endereço (F15-38).
- Nota do sistema dos anexos recusados com autor fixo (`AutoresDoSistema`): não há precedente de autor sistema em `Interacao`; promovida ao ADR 0016, item 10 (D15).
- Concorrência da triagem: quem perde a corrida recebe `409 triagem-ja-tratada`; quem chega depois do item apagado recebe `404` (o item é apagado de fato, como dizem F15-73, F15-76). Agora nos cenários F15-81 e F15-83.
- O fluxo BPMN não foi mexido neste PR: muda no PR que muda o passo (processo §1). O design diz quais (F15f, F15i, F15k).
- Estratégia §8 e §14 mudadas neste PR (a ERS já trouxe a entrada por e-mail para o v1 aqui).
- `CLAUDE.md` (ADRs 0001–0016), `docs/specs/README.md` (coluna Design) e `docs/estado.md` ficam para o orquestrador ou para o G2 (design §2).

## Devolução do /plano, 1ª (25–26/11, "siga com todos")

- Itens do design tratados: 1, 2, 3, 6, 8, 9, 13, 16, 18, 19, 20 (e o RNF-SEG-11 da ERS, só redação). Os da spec ficaram com o analista (`notas/analista.md`).
- F15-71 e F15-72 têm parte de API e parte de tela ("a casca mostra", "o menu não mostra"). Para cada cenário ficar num PR com o "então" inteiro, o F15k leva também o menu, a contagem na casca e a lista (só leitura); o F15o fica com detalhe, ações e Configurações. Isso desvia do "o F15j fica com menu e casca" do handoff. Alternativa, se o orquestrador preferir o F15k só de API: o analista separa cada um em dois cenários, por exemplo "F15-71: pela API, quem tem `solicitacao.criar` lista os itens da empresa, do mais novo, com o motivo; o resumo dá quantos aguardam" e um novo "a casca mostra quantos aguardam"; "F15-72: pela API, `403 sem-permissao`" e um novo "o menu não mostra a triagem a quem não tem `solicitacao.criar`"; os de tela vão para o F15o.
- Cenários que cobrem várias ações (F15-80, F15-81, F15-83, F15-101, F15-102, F15-104) ficam no F15m, o último PR das ações; F15-109 (log de todos os resultados) no F15n, o último da entrada. Os PRs anteriores seguem a regra e testam o próprio caminho.
- "Primeiro deploy do PR que liga a entrada" (F15-26, F15-27): li como o primeiro deploy do PR dono de cada cenário (F15h e F15f), com o Daniel ligando a própria empresa pelo `PUT` (o interruptor só chega no F15o). No F15d tudo cai em "desligado" antes da autenticidade.
- Código das empresas existentes: `gen_random_uuid()` (núcleo do PostgreSQL 13+, `pg_strong_random`) na migration, em vez de `random()`. Preencher em C# exigiria um passo fora da migration e deixaria a empresa só leitura sem endereço (a guarda barra). O texto do D2 ("não se adivinha") fica verdadeiro.
- Expurgos no só leitura (F15-107, F15-108): compatível com o ADR 0015 (item 4 restringe os jobs que operam a empresa; item 3 permite o ponto revisado fora do change tracker que declara o que toca) e com a segurança §5 ("os expurgos" seguem no só leitura). A convenção §1 ganha a menção explícita no F15e. Nenhum ADR aceito muda.
- Promovidos ao ADR 0016 (Proposto): respostas da rota de entrada (item 2), registro de entrada (8), limites (9), autor do sistema (10), expurgos por prazo (11). As regras de código correspondentes entram nas convenções no PR que traz o código (design §2).
- Conferência de "cada cenário em um PR" e dos títulos da spec por script (no scratchpad da sessão): 97 cenários, 97 na divisão, nenhum repetido; títulos batem com a divisão.
