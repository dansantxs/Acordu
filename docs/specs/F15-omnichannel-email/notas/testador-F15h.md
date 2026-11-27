# Notas do testador — F15h (etapa 5)

Cenários F15-20, F15-24, F15-25, F15-26, F15-70, F15-103 (design D6, D10, D11, D14). Sem SDK .NET na sessão: os testes C# só rodam na CI, depois que o bot gerar a migration `TriagemDeEmail` (os de integração e os funcionais dependem das tabelas novas). Lidos contra as regras de análise (nameof, CA1826, CA1859, CA1305, async/await, sem array vazio em `InlineData`). `node scripts/conferir-specs.mjs`: ok.

## Já coberto pelo desenvolvedor (conferido, verifica o "então")
- F15-20/24/25/26: Api (item lido do banco com motivo, clientes possíveis, nenhuma solicitação/mensagem/conversa, nada respondido, nada na outra empresa), Application (`Classificar`, ordem DESCONHECIDO, NAO_CONFIRMADO, MAIS_DE_UM_CLIENTE/CONTATO_INATIVO), integração (`uuid[]`).
- F15-70: Api (todos os campos, chave `{tenant}/triagem/{item}/`, conteúdo no storage, recusado no item, registro TRIAGEM, e-mail ao remetente e à equipe vazios, log sem endereço/assunto/nome de arquivo/texto), Application (sem nota do sistema, sem evento), domínio, integração (só leitura recusa, contexto do contato recusa, portal não vê, classificação, exemplos, ApagadorDeEmpresa devolve a chave e apaga em cascata).
- F15-103: Api (duas entregas, quatro em paralelo sem arquivo órfão, duas empresas), Application (repetida, falha da gravação descarta o storage).
- Testes antigos alterados (IGNORADO para TRIAGEM): conferidos um por um (`ComandosEntradaEmailTests`, `EntradaEmailTests` e `AberturaPorEmailTests` da Api). Só mudou o resultado esperado do remetente não identificado, que é a regra nova (design §3); o descarte (automático, laço, filtro), o token de conversa e o "nada é respondido" seguem com `IGNORADO`/`Registrada` e as mesmas asserções. Nenhuma asserção de aceite foi enfraquecida.

## Acrescentado
- Api (`TriagemEmailTests`): From com vários endereços (inclusive o de um contato) e From inválido/vazio viram DESCONHECIDO sem remetente; automático de não identificado é descartado sem item; sem Message-ID, duas entregas, um item; desconhecido com autenticidade `fail` segue DESCONHECIDO; dois clientes sem autenticidade é NAO_CONFIRMADO sem clientes possíveis.
- Application: `ComandosEntradaEmailTests` (From inválido/vazio/sem local, nome/assunto/texto acima do teto são cortados e não derrubam a entrada, descarte antes da triagem sem item nem upload, contato em cliente inativo e em ativo abre no ativo, dois clientes sem autenticidade) e `IdentificacaoDoRemetenteTests` (três clientes, cliente inativo ao lado do ativo, endereço único inválido).
- Domínio: `ItemTriagemEmailTests` (tetos exatos, acima do teto, motivos sem clientes, exatamente 5 anexos).
- Integração: item com tudo nulo, tetos de coluna (e-mail 254, nome 200, assunto 998, texto 10.000), isolamento por Id entre empresas, `ApagarParaRestaurar` não leva o item nem a chave de outra empresa.

## Observações (ressalvas para a descrição do PR)
- Endereço com acento antes do `@` (`sérgio@...`) é inválido para `Email.Normalizar`: o item vai à triagem como DESCONHECIDO com `RemetenteEmail` nulo, e a equipe só vê o nome (se houver). Aceitável no v1 (a spec fala em remetente "com um endereço válido"), mas a tela do F15k deveria mostrar isso com clareza.
- "Pessoa da equipe que não é contato" cai em DESCONHECIDO (a identificação só olha contatos); não há teste dedicado porque é o mesmo caminho de "ninguém com o e-mail".
- A rota não tem endpoint de leitura da triagem (F15k): "portal sem item interno" e permissão 403 ficam cobertos só pelo filtro de contexto (integração); nenhuma rota nova no PR, então não há 403/404/409 de API a testar.
- Os testes de integração e os funcionais precisam da migration `TriagemDeEmail` (ainda do bot); até lá só os de unidade e de arquitetura rodam.
- Nada foi executado em .NET; a conferência real é a CI.
