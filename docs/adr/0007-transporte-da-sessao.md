# ADR 0007 — Transporte da sessão: access token em memória, refresh token em cookie HttpOnly

- **Status:** Aceito
- **Data:** 09/10/2026
- **Contexto do projeto:** Acordu (v1)
- **Complementa:** ADR 0007 do Saldu (Identity + JWT curto + refresh revogável), herdado pelo [ADR 0001](0001-herdar-decisoes-do-saldu.md). Aquele ADR decide **o que** é a sessão; este decide **por onde** cada token viaja e onde fica guardado.

## Contexto

A sessão tem dois tokens:
- **access token** (JWT de 60 min), que carrega tenant, papel, permissões e, para Cliente, `cliente_id` e `escopo_portal`;
- **refresh token** (opaco, 30 dias, persistido como hash e revogável), que só serve para obter um novo access token.

O herdado do Saldu não dizia como os tokens chegam ao navegador nem onde ficam. A implementação (`/auth/*`) escolheu cookie HttpOnly para o refresh, e isso estava registrado só no contrato da API (§2). Como a escolha condiciona o frontend, o deploy (mesmo site) e a defesa contra XSS e CSRF, ela precisa de registro próprio.

As ameaças que pesam:
- **XSS:** qualquer token legível por JavaScript pode ser roubado por um script injetado. O refresh é o mais valioso, porque dura 30 dias e gera novos access tokens.
- **CSRF:** um cookie enviado automaticamente pelo navegador pode ser usado por outro site para disparar ações em nome do usuário.
- **Roubo de refresh:** precisa ser detectável e revogável.

## Decisão

1. **Refresh token só em cookie**, nunca no corpo de resposta:
   - nome `acordu_refresh`; `HttpOnly`, `Secure`, `SameSite=Strict`, `Path=/api/v1/auth`, validade de 30 dias;
   - JavaScript não lê o cookie (XSS não o rouba), outros sites não o enviam (`SameSite=Strict`), e ele só viaja para as rotas de autenticação (`Path`).
2. **Access token no corpo** das rotas que abrem sessão (`registro`, `login`, `refresh`, `tenant-ativo`, aceite de convite), no formato `{ accessToken, expiraEm }`, e enviado pelo SPA no cabeçalho `Authorization: Bearer`.
   - O SPA guarda o access token **só em memória** (variável do cliente HTTP). Nunca em `localStorage`, `sessionStorage` ou cookie legível.
   - Ao recarregar a página, o SPA chama `POST /auth/refresh`; o cookie vai sozinho e a resposta traz um novo access token.
3. **Rotação com detecção de reuso:** cada `refresh` consome o token atual e emite outro (novo cookie). Reapresentar um refresh já consumido indica cópia: todas as sessões do usuário naquele tenant são revogadas e a resposta é `401`.
4. **Logout e troca de tenant** revogam o refresh atual; o logout apaga o cookie.
5. **CSRF:** como só as rotas de `/api/v1/auth` recebem o cookie, todas são `POST` e o cookie é `SameSite=Strict`, não há token anti-CSRF no v1. As demais rotas usam `Bearer`, que o navegador não envia sozinho.
6. **Sessão de link do portal** (RF-POR-08) não tem refresh nem cookie: só um access token de 60 min no corpo. Expirou, o contato usa o link de novo.
7. **Pré-requisito de deploy:** SPA e API no **mesmo site** (`acordu.app.br`, a API em `/api` pelo Caddy), para o cookie ser first-party. Separar em domínios diferentes exige rever este ADR.

## Consequências

**Positivas**
- O token de longa duração fica fora do alcance de JavaScript; um XSS ainda é grave, mas rouba no máximo um access token de 60 min.
- Sem estado de sessão no servidor além da tabela de refresh tokens; a API continua stateless para as rotas de negócio.
- Logout real e "derrubar todas as sessões" funcionam, porque o refresh é persistido e revogável.

**Negativas / riscos**
- **Abas concorrentes:** duas abas que fazem `refresh` ao mesmo tempo apresentam o mesmo token; a segunda é tratada como reuso e derruba as sessões. **Mitigação obrigatória no SPA:** serializar o refresh entre abas (Web Locks API, com `BroadcastChannel` para repassar o novo access token às outras abas) e nunca repetir automaticamente um `refresh` que falhou por rede. Se isso se mostrar insuficiente, a alternativa é uma janela de tolerância curta no servidor (aceitar o token recém-consumido por poucos segundos, devolvendo o mesmo sucessor) — a decidir com medição, registrando aqui.
- **Mudança de papel** só vale no próximo refresh (até 60 min), porque as permissões estão no access token. O portal já confere vínculo e escopo no banco a cada requisição (segurança §5.1); para a equipe, a janela de 60 min é aceita no v1.
- **App mobile** (fase futura) não usa cookie de navegador: vai precisar receber o refresh no corpo e guardá-lo no armazenamento seguro do sistema (Keychain/Keystore), por um fluxo próprio. Fica para o ADR do mobile.
- **Ambiente de desenvolvimento** precisa de HTTPS ou de exceção local para `Secure` funcionar; documentado no README quando o SPA existir.

## Alternativas consideradas

- **Tudo em `localStorage`** (access e refresh): simples, mas um XSS rouba o refresh e a conta por 30 dias. Rejeitado.
- **Sessão só por cookie** (access token também em cookie, sem `Bearer`): protege do XSS, mas expõe todas as rotas a CSRF e exigiria token anti-CSRF em cada escrita. Rejeitado.
- **BFF** (backend-for-frontend que guarda os tokens e expõe só um cookie de sessão): a opção mais forte, mas adiciona um componente e estado de sessão no servidor. Desproporcional para o v1; reavaliar se houver exigência de cliente corporativo.
- **Refresh no corpo, guardado em memória:** o usuário perderia a sessão a cada recarga da página. Rejeitado.
