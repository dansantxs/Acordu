# Notas do arquiteto — F15 (etapa 3)

Premissas do design (25/11/2026). O que precisa de conferência fora do código está marcado "conferir" e tem PR dono no design §3.

## Dúvida de cenário (NEEDS_CLARIFICATION)

- **F15-79** diz que o contato (conta ou link) que tenta a triagem recebe `404`, "como as demais rotas da equipe". Hoje as rotas da equipe respondem **`403`** ao contato: ADR 0002, item 4 ("endpoints internos rejeitam papel Cliente (403)"), contrato §14 ("403 … papel Cliente em rota interna"), `Politicas.Equipe` e os testes funcionais (ex.: `AtividadesTests`, F13-25). Proposta: "`403`, como as demais rotas da equipe (ADR 0002, item 4)". Dar `404` só na triagem contradiz um ADR aceito; o design não decide. O cenário ficou no F15h, pendente.

## Fatos de provedor a conferir (não verificados nesta etapa, sem acesso aos painéis)

- Cloudflare Email Routing: subdomínio (`entrada.acordu.app.br`) com regra *catch-all* própria; Worker chamado uma vez por destinatário (o F15-21 depende disso: e-mail para A e B no mesmo domínio); erro lançado no Email Worker vira falha temporária ao servidor de origem (reenvio); limite de 25 MiB; carimbo `Authentication-Results`/`ARC-Authentication-Results` visível no `message.headers` do Worker. Dono: F15d (provisionamento e e-mail real). Se o carimbo não vier, D11 (b) antes do F15e.
- Cloudflare: não há nota de spam nem antivírus expostos ao Email Worker no plano gratuito (premissa; registrada como risco no D11 e no ADR 0016).
- Resend: aceita `headers` personalizados no `POST /emails` (`Auto-Submitted`, `In-Reply-To`, `References`). Um `Message-ID` próprio não é necessário (D3 reconhece a resposta pelo destinatário). Dono: F15c.
- Gmail: agrupar a conversa com o assunto `Re: {assunto} (#N)` (D4). Dono: F15e.
- Inbound do Resend (D1 b): formato e cota do plano gratuito não conferidos; por isso é só alternativa.

## Premissas de projeto

- Os Workers ficam em `infra/cloudflare/` (o handoff citava `workers/`, que não existe); o novo é `infra/cloudflare/email-entrada/`.
- MimeKit (MIT) é biblioteca de protocolo, não SDK de provedor (ADR 0010 §10, como o `AWSSDK.S3`).
- Remetente = cabeçalho `From` (o que o DMARC alinha); envelope só para reconhecer devolução.
- O token da conversa e o código da empresa ficam em claro: são roteamento, não credencial (a entrada exige remetente contato ativo do cliente e autenticidade). A convenção de "só o hash" continua para segredos de uso único.
- Uma conversa por solicitação (não por contato): o colega em cópia responde ao mesmo endereço (F15-38).
- Nota interna dos anexos recusados com autor fixo do sistema: não há precedente de autor sistema em `Interacao`; a alternativa (nota com o autor da equipe) não existe, porque ninguém da equipe agiu.
- Concorrência da triagem: quem perde a corrida recebe `409 triagem-ja-tratada`; quem chega depois do item apagado recebe `404` (o item é apagado de fato, como dizem F15-73, F15-76).
- O fluxo BPMN não foi mexido neste PR: muda no PR que muda o passo (processo §1). O design diz quais (F15e, F15g, F15h).
- Estratégia §8 e §14 mudadas neste PR (a ERS já trouxe a entrada por e-mail para o v1 aqui).
- `CLAUDE.md` (ADRs 0001–0016), `docs/specs/README.md` (coluna Design) e `docs/estado.md` ficam para o orquestrador ou para o G2.
