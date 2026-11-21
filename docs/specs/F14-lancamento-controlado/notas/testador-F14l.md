# Notas do testador (F14l, etapa 5)

Sem SDK .NET na sessão: nada foi compilado nem rodado; os testes .NET rodam só na CI. A migration `DemonstracaoDaEmpresa` ainda não existe: os testes com banco falham até o bot gerá-la.

## Cobertura
Os cenários F14-65 a F14-69, F14-71 e F14-106 a F14-108 já tinham testes do desenvolvedor que verificam o "então" (Domain, Application, guarda na integração, funcionais). Coluna Teste da spec preenchida.

## Defeito de teste achado e corrigido
`AreaNebrysTests.DocumentoNebrys_SoAsRotasDaArea` exigia só 3 caminhos no `nebrys.json`; o F14l adicionou 6. Atualizado.

## Testes escritos
- `AreaNebrysTests.RotaDaArea_*`: rotas novas (prorrogação, cancelamento, reativação, liberação POST e GET) na barreira 404 (F14-63).
- `AcoesNebrysTests`: `Prorrogar_AteHoje_FimNoInicioDeAmanha`, `Acao_Feita_TrilhaComANomeDaAcaoEOAntesEDepois` (ativação, prorrogação, reativação), `Acao_OperadorTiradoDaLista_404ENadaMuda`, `Liberar_SemSessao_404ENadaMuda`.

## Ressalvas
- Prorrogar em fuso diferente do padrão só tem teste de unidade (Domain e Application); o funcional usa o fuso padrão.
- Não há teste do IP gravado na trilha das ações.
- OpenAPI escrito à mão: o `ContratoOpenApiTests` pode pedir regeneração depois da migration.
