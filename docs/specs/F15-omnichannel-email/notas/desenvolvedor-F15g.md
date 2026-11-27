# Notas do desenvolvedor — F15g (etapa 4)

Premissas técnicas do PR dos anexos da abertura por e-mail (F15-14, F15-15, F15-98; design D6, D8, D14 e D15; ADR 0016, item 10). Sessão sem SDK .NET: o backend foi lido com cuidado, mas só compila e roda na CI. O front rodou aqui (`pnpm test`, `lint`, `format:check`, `build`).

## Anexos um por um (D14)

- **O que é anexo:** o `LeitorMime` percorre a árvore do MIME e entrega, decodificada e na ordem, toda parte que não é corpo (`AnexoDeEntrada`: nome como veio, tipo declarado, bytes). Corpo = `TextPart` `text/plain` ou `text/html` que não é anexo (`Content-Disposition: attachment`); todas as partes assim ficam de fora, não só a primeira (o Apple Mail divide o corpo em várias em volta das imagens). A imagem embutida (`cid:`, sem `attachment`) entra como anexo (pergunta 4). `text/plain` com `attachment` é anexo.
- **Cifrado e assinado:** as partes dentro de um `multipart/encrypted` saem com o tipo dele (o conteúdo é texto blindado ASCII, que a detecção aceitaria como `text/plain`); a assinatura de um `multipart/signed` sai com o próprio tipo (`application/pgp-signature`, `application/pkcs7-signature`). A Application recusa pelo tipo declarado: `message/*`, `multipart/encrypted`, `application/pgp*`, `application/pkcs7*`, `application/x-pkcs7*` (o design diz "sem tipo permitido"; a assinatura PGP é texto e passaria pela detecção). Sem o tipo `MultipartEncrypted` do MimeKit (não sei se o `MimeKit.Core` traz a criptografia): pelo `ContentType`.
- **Mensagem anexada** (`MessagePart`): não é decodificada; vai só com o nome e o tipo, e é recusada.
- **Ordem das conferências** (`AnexosDaEntrada.Separar`, função pura): tipo declarado → tamanho (acima de 20 MB, como o portal confere primeiro) → vazio → tipo pelo conteúdo (`TiposDeArquivo`, a mesma da `PoliticaAnexos` do portal) → até o 5º válido; recusado não ocupa vaga. **Arquivo vazio** é "tipo não permitido" (não há tipo a detectar; o cenário lista três motivos, e um quarto só para isso pesaria no texto ao contato).
- **Nome:** último trecho depois de `/` ou `\`, sem caracteres de controle, aparado, até 255 mantendo a extensão (até 16 caracteres) e sem partir par substituto. Vazio, `.` ou `..` → `anexo-N` (posição no MIME, a partir de 1) com a extensão do tipo declarado só para imagens, PDF e `message/rfc822` (`.eml`). Sem isso o `Anexo.Criar` recusaria o nome e o e-mail inteiro viraria `5xx` e reenvio.
- **Upload:** os válidos viram `ArquivoRecebido` e sobem pelo `RecebimentoAnexos.EnviarAsync` (que confere de novo pela mesma política, então nunca recusa o que passou), com a chave `{tenantId}/{solicitacaoId}/{anexoId}`, antes do commit; o Id da solicitação é sorteado antes. Qualquer falha da gravação (inclusive a corrida de duas entregas iguais no índice do registro) chama o `DescartarAsync`, como no portal. Só sobem quando o e-mail abre a solicitação: o não identificado e a resposta continuam só no registro (F15h, F15i).
- **Memória:** o MIME inteiro já está na memória do MimeKit (até 25 MiB); as partes são copiadas em `byte[]` antes de o `MimeMessage` ser descartado.

## Nota do sistema (D15)

- `AutoresDoSistema` em `Domain.Common`: `EntradaEmail` (Id fixo `acd00000-0000-4000-8000-00000000e001`), `NomeEntradaEmail` ("Acordu (e-mail recebido)", o nome do design), `Eh` e `Nome`. O nome fica no domínio, ao lado do Id, para a consulta e qualquer outra leitura usarem a mesma fonte.
- `Interacao.RegistrarNotaDoSistema`: INTERNA, `AutorEhCliente = false`, sem anexos, texto obrigatório, autor do sistema (senão `ArgumentException` em `autorId`), canal da entrada (nunca `ACORDU`). **Não registra `InteracaoRegistrada`**: é o jeito mais simples de cumprir "os manipuladores de aviso ignoram interação de autor do sistema" (D15) em todos de uma vez (aviso ao contato, aos seguidores, feed, seguimento automático, primeira resposta). O sinal de tempo real continua, porque vem do change tracker. O `Registrar` das pessoas passou a recusar o Id do autor do sistema.
- Texto da nota: "Anexos do e-mail que não entraram na solicitação:" e uma linha por recusado, `- {nome}: {motivo}`; até 20 linhas e "- e mais N arquivos" (um e-mail de 25 MiB pode ter milhares de partes, e o texto tem 10.000 caracteres). Mesma regra no aviso. Com todos recusados, a solicitação nasce só com a nota (sem a mensagem do contato).
- A nota é acrescentada pelo `ComandosEntradaEmail` (que ganhou o `RecebimentoAnexos` e o `IRepositorio<Interacao>`), não pelo `AberturaPeloContato`: o núcleo comum ao portal não ganha conceito do e-mail.
- **DTO:** `InteracaoDto.AutorSistema` logo depois de `AutorEhCliente`; `autor` com o Id fixo e o nome fixo; o Id do sistema fica fora do `Nomes.DeAutoresAsync`. OpenAPI à mão no formato do gerado e `pnpm api:tipos`. O portal não muda: só lê EXTERNAS, e o teste confere que o DTO dele não ganhou o campo.
- **Front:** a mensagem com `autorSistema` mostra o nome da API, "Nota do sistema" no lugar de "Nota interna", fundo neutro com borda tracejada (não o âmbar das notas das pessoas) e o texto puro, sem ler menções (a lista vem de nomes de arquivo do e-mail). Só a conversa da equipe; o portal nunca recebe a nota.

## Recusados no aviso de recebida (D6)

- `EmailRecebido` ganhou `SolicitacaoId` (obrigatório com `SOLICITACAO` e proibido nos outros; sem FK, o registro vive 7 dias) e `Recusados` (`AnexosRecusados`, VO com a lista de `AnexoRecusado` nome + `MotivoRecusaAnexo`, só com `SOLICITACAO`), em `jsonb` pelo `AnexosRecusadosConverter` (molde do `PoliticaSlaConverter`, motivo em SCREAMING_SNAKE pelo `EnumTexto`). Sem índice novo: a consulta roda uma vez por abertura, numa tabela de 7 dias, e o F15n põe os índices das contagens.
- `IEmailsRecebidos.RecusadosDaAberturaAsync(solicitacaoId)` (sob os filtros do tenant). O `AvisosAoContato` consulta só na origem EMAIL, no aviso de recebida, e acrescenta o trecho depois do "Guarde este número…", antes do link: a lista, uma linha por arquivo com o motivo entre parênteses, e "Se precisar deles, envie em outro formato ou por um link.". A nota (INTERNA) nunca é lida pelo aviso (RNF-SEG-03).
- **Migration:** `AnexosRecusadosDoEmail`, pedida ao bot por `.github/migration-request`. Duas colunas novas, nulas (`solicitacao_id uuid`, `recusados jsonb`), sem preenchimento: nada a acrescentar no `Up`.

## Testes e arquivos que mexem em testes anteriores

- `ComandosEntradaEmailTests` e `AvisosAoContatoConversaTests` (do F15d/F15f) só ganharam as dependências novas no construtor e o método novo nos fakes do `IEmailsRecebidos`; o motivo de uma asserção perdeu o "(os anexos são do F15g)". `ArmazenamentoFalso` (`Falsos.cs`) passou a registrar os envios.
- Funcionais em `AnexosPorEmailTests` (MIME `multipart/mixed` montado no teste; a parte de mais de 20 MB vai em `binary`, para o e-mail caber nos 25 MiB da rota).

## Documentos

- Contrato §7 (`autorSistema` e a nota do sistema), §12a (recusados no aviso de recebida), §18 (anexos da abertura e o registro); modelo §4.5 (nota do sistema, diagrama), §4.15 e §5 (`AnexosRecusados`); read side §8c; segurança §7 (anexos do e-mail); convenções §2 (autor do sistema) e §5 (anexos do e-mail recebido); `estado.md`; coluna Teste da spec.
- Sem mudança: ERS (o RF-SOL-11 já traz os anexos dentro da política, e a decisão da pergunta 4 está no G2), ADRs (o item 10 do 0016 já é a nota do sistema), fluxo BPMN (nenhum passo, papel ou status novo), deploy e setup (sem variável nova), estrutura do repositório (sem pasta nova).
- Manual e Novidades: a nota do sistema só aparece com o recebimento ligado, que chega a quem usa no F15o; como no F15a, a ajuda ganha o e-mail no PR que liga a entrada.

## Menores do `/plano` (`plano-2.md`)

- Nenhum dos 4 a 11 é do F15g: 5 é o aviso de anexos recusados na resposta (F15j); 6, os casos da triagem e da resposta (o MIME malformado já responde `202`, F15d); 7, os limites (F15n); 4, 8, 10 e 11, já tratados ou de outros PRs.
