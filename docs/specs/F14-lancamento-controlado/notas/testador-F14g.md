# Notas do testador (F14g)

- Cobertura conferida para F14-56 a F14-59 (coluna Teste da spec preenchida). CPF e CNPJ, com e sem pontuação, alfanumérico em minúsculas, espaços, empresa expurgada, liberação usada uma vez, concorrência (4 cadastros), HMAC (hex, muda com a chave, sem documento em claro) e canal na mensagem já estavam cobertos.
- Acrescentei: `Registro_Recusado_NaoMudaNenhumaTabela` (contagens de usuários, empresas, registros de documento e sessões iguais antes e depois da recusa) e `Api_SemChaveHmacOuComChaveCurta_NaoSobe` (a montagem da Api falha, não só o `ProtecaoDocumento`).
- Ressalva: F14-71 (liberação pela Nebrys) fica para o F14l; aqui a liberação é gravada direto no banco.
