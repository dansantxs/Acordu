# Notas do testador (F14i, etapa 5)

Cenários F14-29 a F14-38, F14-104 e F14-112: todos com teste em `Acordu.Api.FunctionalTests/ExpurgoEmpresasTests`, cada um verificando o "então" (banco por tabela com `tenant_id`, storage por prefixo, 401, contagens do diário). Coluna Teste da spec preenchida.

Acrescentei:
- `Diario_PessoaComSessaoEmOutraEmpresa_FicaComAConta` (F14-32, o caso da sessão; antes só havia vínculo e contato).
- `Diario_SoLeituraSemDataDeInicio_NaoEApagada` (borda: sem `SomenteLeituraDesde` não há de onde contar).

Bordas conferidas: limite exato do prazo apaga (F14-29, `agora - prazo`) e um minuto antes não (F14-30); VENCIDA e CANCELADA em ambos; ATIVA com data antiga de só leitura e TESTE velho nunca (F14-31); `ExpurgoDevido` tem testes de unidade.

Ressalva: F14-31 cobre TESTE e ATIVA; a demonstração ainda não existe (F14s) e é ATIVA por design, então fica coberta indiretamente.

## Rodada 1 (revisão do revisor)

- F14-104, decisão "só apagar a linha": `Registro_LiberacaoUsadaPorCadastroAbandonadoExpurgado_LiberacaoNaoVoltaE400` verifica o "então" (a linha do primeiro cadastro fica, `LiberacaoDisponivel` falso, novo registro 400). `Registro_DocumentoDeCadastroAbandonadoExpurgado_201` cobre a linha que sai e o 201.
- `Registro_DocumentoNovo_RegistroEEmpresaComOMesmoInstante` usa relógio que anda a cada leitura: falharia se o cadastro lesse o instante duas vezes. Verifica de fato o vínculo `PrimeiroTesteEm == CriadoEm`.
- `Diario_EmpresaAbandonadaESoLeituraHaOPrazo_...` confirmado na coluna Teste do F14-104 (empresa nos dois passos: sai pelo expurgo de cadastros, linha do documento apagada, 201 no registro novo).
- `Diario_ContaCriadoraDeOutraEmpresa_NaoEApagada` (F14-32) e `Diario_ContaEmDuasEmpresasExpurgaveis_EApagadaAoSairDaSegunda` (F14-29/32) verificam conta mantida e conta apagada, respectivamente, sem vínculo nem sessão sobrando.
- Coluna Teste da spec ampliada com esses testes (F14-104, F14-32, F14-29).
