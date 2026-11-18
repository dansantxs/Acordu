# Notas do testador (F14j, etapa 5)

- Cobertura completa dos F14-39 a F14-43, F14-101 a F14-103 e F14-113 em `AvisosSituacaoTests` (funcional, o diário de ponta a ponta com e-mails verificados na caixa de testes).
- Testes acrescentados: destinatários de fora (papel Leitor, contato do portal, Dono de outra empresa no mesmo diário, com as marcas isoladas por tenant) e datas no fuso da empresa (America/Manaus, onde a data diverge de São Paulo).
- Já cobertos pelo desenvolvedor: ATIVA nunca recebe, falha de envio (conta em `falhas`, sem marca, o próximo diário reenvia), configuração padrão, configurável e inválida (a API não sobe).
- Ambiente: `pnpm test` tem 3 falhas antigas desta máquina (anexo/logotipo; nota do F14a), fora do escopo.
- Sem defeito no código de produção.

## Rodada 1 (após os achados do revisor)

- Instante x dia civil: os testes passaram a expor a diferença. Nos unitários (`TenantTests`), o diário de 29/10 às 06:40 UTC está a mais de 7 x 24 h do fim (cadastro às 13:00 UTC) e mesmo assim manda o aviso de 7 dias, e o de 28/10 não manda; o caso Manaus x São Paulo muda o aviso (1 x 7 dias) para o mesmo instante. Nos funcionais, `AteODiario` roda o diário às 06:40 UTC com o cadastro às 13:00 UTC: o aviso sai no dia civil certo e não no dia anterior nem no seguinte. A conta antiga (fim - N dias <= agora) falharia nos dois.
- Meus dois testes (destinatários de fora; datas no fuso Manaus) continuam valendo: não dependem da hora do diário (o aviso de 7 dias já é devido) e o de fuso confere a data do texto contra a conta em Manaus.
- `Diario_SemDestinatario_NadaEMarcadoEOAvisoSaiQuandoHaQuemReceba` entrou na coluna Teste do F14-43 (único Dono possível é o criador).
