# Notas do testador — F15c

Sem SDK .NET: testes C# só validados na CI (lidos com cuidado). `conferir-specs` ok; `pnpm test` do web: 775 verdes.

Cobertura: F15-51 a F15-55 e F15-57 já tinham testes que verificam o "então" (funcional, unitário, Resend, SMTP/Mailpit, persistência, isolamento por tenant e por cliente). Lacunas encontradas e cobertas em `tests/Acordu.Application.UnitTests/Notificacoes/AvisosAoContatoConversaTests.cs`:
- recebimento gravado como ligado mas sem domínio de entrada: aviso como desligado, sem conversa, Reply-To da empresa (o funcional sempre tem domínio);
- a conversa é gravada antes do envio e reusada se já existe (um token por solicitação);
- corrida: a gravação da conversa falhando (índice único) lança antes de qualquer envio.

Observações (ressalvas):
- O índice único da corrida é provado só por `PersistenciaTests.ConversaPorEmail_SegundaDaMesmaSolicitacao_IndiceUnicoRecusa` e pela ordem no unitário; não há teste de duas threads de verdade.
- Falha na gravação da conversa acontece depois de o link sem senha já ter sido gravado: na repetição da outbox um novo link é emitido e o anterior revogado (comportamento aceitável, só registrado).
- Reply-To e cabeçalhos em e-mail real, e a migration `ConversasPorEmail` (bot), ficam para conferência no deploy/CI.
