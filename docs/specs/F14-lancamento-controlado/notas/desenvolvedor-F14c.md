# Notas do desenvolvedor (F14c, etapa 4)

Premissas técnicas tomadas no F14c. Nenhuma muda cenário nem decisão do design.

- **Tipo pela rota (D3):** o `ManipuladorExcecoes` troca a `EmpresaSomenteLeituraException` por `422 portal-indisponivel` quando o caminho começa por `/api/v1/portal` ou `/api/v1/auth/portal` (caminho original da `IExceptionHandlerPathFeature`). Fora disso, `empresa-somente-leitura`. Assim o aceite do convite da equipe (`/auth/convite`, F14-97) fica com o tipo da equipe e o do portal (`/auth/portal/convite`, F14-98) com o neutro, sem olhar a sessão.
- **Mensagem neutra:** título "Envio indisponível" e `detail` "No momento não é possível enviar por aqui; fale com {prestador}". O nome é o do portal (`NomePortal`) ou o da empresa, o mesmo que o portal mostra. Vem na própria exceção (`NomePrestador`), lido pela guarda na mesma consulta da situação (`NomePortal ?? Nome` quando só leitura, nulo quando grava): não há consulta a mais nem leitura da Api ao banco no tratamento do erro. Sem nome (exceção lançada por outro ponto), "fale com o prestador do serviço".
- **`envioDisponivel`** no `/portal/eu`: `!Situacao.SomenteLeitura()` (`SituacaoEmpresaExtensions`). Não considera o `EnvioLiberado` do F9, que trata de e-mail a terceiros e não do que o contato envia pelo portal.
- **Convites antigos (F14-97, F14-98):** nada mudou no aceite. A conta nova é criada pelo `UserManager` dentro da transação aberta pelo aceite; a guarda barra o `Vinculo` (da empresa) no `SaveChanges` final e a transação é descartada sem commit, levando a conta, o vínculo, o contato ligado e o consumo do convite. A conferência de senha de conta existente fica antes da transação, como já era (lockout gravado). Os testes conferem que o convite segue válido aceitando-o depois que a empresa volta a ATIVA.
- **Link antigo (F14-99):** a troca grava só `RegistroAuditoria` (rastro) e passa; nada mudou no código.
- **Anexo do contato recusado:** o arquivo já enviado ao storage sai pela limpeza que o `ComandosPortal`/`RecebimentoAnexos` já faz em qualquer falha (conferido no teste).
- **OpenAPI:** `portal.json` e `portal.d.ts` ganharam `envioDisponivel`. O `interno.json` e o `interno.d.ts` foram regravados sem mudança de conteúdo (só fim de linha no Windows).
- **Front:** só os tipos gerados. A faixa e as ações desabilitadas do portal são do F14e.
