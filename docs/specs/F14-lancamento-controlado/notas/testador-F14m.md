# Testador, F14m (área da Nebrys no SPA)

Parecer: aprovado. Sem defeito no código de produção.

## F14-64
Já coberto pelo desenvolvedor: sessão da equipe (nas quatro rotas, com e sem sessão de operação aberta) e sem sessão (vai para a entrada sem chamar a API). Faltava o contato do portal e a sessão por link, e a raiz `/nebrys` com cada uma. Escrito em `apps/web/src/nebrys/guarda.test.tsx`: contato do portal e link nas quatro rotas (não encontrado, sem heading da área, nenhuma chamada a `/api/v1/nebrys`), os dois com operação aberta (a operação é encerrada) e endereço inexistente da área sem sessão (vai para `/nebrys`).

## Fluxos conferidos (já cobertos em `area.test.tsx` e `sessaoOperacao.test.ts`)
`If-Match` com a versão lida (ativar, prorrogar); 409 relê e avisa; 422 mostra a explicação; 404 diz que a empresa não existe mais; liberação 204, 422, documento inválido; sessão vencida; sair; lista recusada pela barreira; expiração por temporizador.

## Acrescentado em `apps/web/src/nebrys/seguranca.test.tsx`
- Entrar: token fora de cookie, `localStorage`, `sessionStorage` e cache de consultas; senha fora das variáveis de mutação.
- Liberar: documento fora do cache de mutações e de consultas, do cookie e do armazenamento (o endereço já era conferido).
- Liberar com 400 e `errors`: erro no campo, sessão mantida.

## ESLint
Conferido com arquivos temporários (removidos): `features`, `portal` e `shared` importando `@/nebrys/*` falham; `nebrys` importando `@/features/*` ou `@/portal/*` falha. Sem teste automatizado próprio: a regra é verificada pelo `pnpm lint` só quando alguém viola; ressalva menor.

## Observações
- 403 e isolamento entre tenants não se aplicam ao front (a decisão é da API; coberta no F14k/F14l).
- `indisponivel` no refresh é tratado como sem sessão da empresa (premissa do desenvolvedor): a entrada aparece e a API decide. Aceito.

## Execução
`pnpm test`, `pnpm lint`, `pnpm format:check`, `pnpm build` em `apps/web`: 0. `node scripts/conferir-specs.mjs`: 0.
