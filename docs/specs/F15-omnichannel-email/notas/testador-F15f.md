# Notas do testador — F15f (etapa 5)

Sem SDK .NET nesta sessão: os testes C# foram lidos com cuidado (nameof, CA1859, CA1305, async/await), mas só compilam e rodam na CI. Rodaram aqui: `node scripts/conferir-specs.mjs` (ok), `pnpm test` (777 verdes), `pnpm lint` e `pnpm format:check` (ok).

## O que já cobria (conferido contra o "então")

Os 13 cenários tinham teste que verifica o resultado observável: `AberturaPorEmailTests` (Api, pela rota de entrada como o Worker entrega), `ComandosEntradaEmailTests`, `ConteudoDaAberturaTests`, `IdentificacaoDoRemetenteTests`, `CanaisDeAvisoTests`, `EntradaEmailTests` (integração: leitura do MIME e `ContatosDeEntrada` com isolamento por tenant), `fila.test.tsx` e `detalhe.test.tsx`. Os avisos despachados antes do `202` são exercitados porque os testes da Api leem a caixa logo depois da entrega. O portal não regrediu: `PortalTests.Abrir_OrigemPortal_ComOContatoLogadoEOContratoVigente` e `CanalDasMensagensTests.AbrirNoPortalComAnexo_*` continuam valendo sobre o núcleo comum.

## Testes escritos nesta etapa

- `AberturaPeloContatoTests` (unit, novo): o núcleo comum, para PORTAL e EMAIL: próximo protocolo, sem responsável, contato, um contrato vigente (contrato e SLA), dois vigentes (sem), anexos como primeira mensagem EXTERNA do contato no canal da entrada, sem anexos nenhuma mensagem.
- `AvisosAoContatoConversaTests` (unit): origem EMAIL leva o fio e o endereço da conversa; com o recebimento desligado, só o fio (sem criar conversa); sem fio guardado; desligado e sem conversa; origens PORTAL e INTERNA nunca levam o fio.
- `ComandosEntradaEmailTests` (unit): Message-ID maior que 998 abre com a conversa sem fio; e-mail sem assunto guarda o assunto original vazio.
- `AberturaPorEmailTests` (Api): sem Message-ID (abre e os avisos saem sem `In-Reply-To`); sem assunto (`Re: (#N)`); `From` em maiúsculas e com nome de exibição; corpo com caractere nulo; remetente não identificado (contato inativo, cliente inativo, contato em dois clientes, vários `From`, desconhecido): nenhuma solicitação, nenhuma conversa, registro IGNORADO, nada respondido nem avisado à equipe.
- Coluna Teste da spec atualizada (F15-10, 12, 13, 16, 50).

## Ressalvas para a descrição do PR

- Tudo C# só roda na CI; se um teste novo falhar lá, é defeito a investigar, não ajuste de teste. Os mais sujeitos a surpresa: o corpo com `\0` (depende de o MimeKit preservar o caractere até o `ConteudoDaAbertura`) e o contato em dois clientes via API (assume que a API aceita o mesmo e-mail em clientes diferentes).
- F15-16 × F15-57: o texto do aviso de recebida é marcadora, "Recebemos sua solicitação #N", texto de sempre. A spec diz "o texto começando por"; a leitura do desenvolvedor (marcadora antes) é razoável e está testada, mas o Daniel deve confirmar.
- A conversa no Gmail e no Outlook (F15-16, F15-50) e o redirecionamento real (F15-27) seguem à mão no primeiro deploy, como a spec diz.
- Não se aplicam a este PR: 403, `If-Match` e portal sem item interno (a rota é de token do Worker; a abertura não cria item interno).
