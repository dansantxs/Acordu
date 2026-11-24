# Notas do testador (F14q, etapa 5)

Revisei os testes do desenvolvedor contra F14-44, F14-46, F14-109 e o checklist pedido. Não faltou teste: não escrevi nenhum, e a coluna Teste da spec já estava preenchida e confere com os nomes reais.

Cobertura confirmada: modo CONVITE/ABERTO; modo ausente + `errors.convite` troca para a lista; convite válido (e-mail só leitura, token em `convite`, token apagado); 401 e sem token; e-mail com conta (F14-105); lista com Turnstile na ação `lista-espera`, 202, 400 em `email` (descartável) e 429; link só da política e sumindo com textos não publicados; telas da área (lista, novo, 400/422, detalhe, reenviar, revogar, 404, convidar 422, descartar, 409); `repetirConsulta` sem repetir 4xx; `lerSessaoOperacao` sem encerrar.

Ressalvas:
- `lerSessaoOperacao` é testado direto, não pela renderização da `CascaNebrys` com sessão vencida (o teste da casca só confere os links).
- Sem E2E do modo convite (decisão do desenvolvedor, justificada).

Resultado: `conferir-specs` ok; `pnpm test` 664 passam, 1 falha (a aceita, capturas); `pnpm lint` e `pnpm format:check` ok.
