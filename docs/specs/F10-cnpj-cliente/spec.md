# Spec — F10: preenchimento do cliente pelo CNPJ

| | |
|---|---|
| **Status** | Concluída |
| **Data** | 04/11/2026 |
| **Requisitos** | RF-CLI-08 (decidido em 04/11), RF-CLI-01 |
| **ADRs** | 0010 (sem SDK de provedor), 0011 |

## 1. Objetivo

Ao digitar o CNPJ no cadastro do cliente, o formulário busca os dados abertos da Receita e preenche o que estiver vazio, avisando quando a empresa não está ativa. A consulta sai do navegador, como a do CEP (segurança §9), e falha nunca impede o cadastro.

## 2. Cenários de aceite

"Consultar" = a chamada do navegador ao provedor escolhido na decisão 1, sem cookie e sem Referer, só com o CNPJ na URL. Os textos entre aspas são propostas; os nomes de campo são os do formulário de hoje (`JanelaCliente`).

### Consulta e preenchimento (F10a)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F10-01 | formulário de cliente novo, vazio | a pessoa completa um CNPJ válido de empresa ativa com nome fantasia | preenche razão social, nome (= nome fantasia) e o endereço (CEP, logradouro, número, complemento, bairro, cidade, UF), e telefone e e-mail se vierem (decisão 2) | `clientes.test › CNPJ completo preenche nome, razão social, contato e endereço` |
| F10-02 | formulário de cliente novo, vazio | o CNPJ é de empresa sem nome fantasia | o nome recebe a razão social (decisão 2) | `clientes.test › sem nome fantasia, o nome recebe a razão social` |
| F10-03 | nome, razão social, e-mail ou telefone já digitados | a consulta responde | o que já foi digitado fica como está; só os campos vazios recebem dados | `clientes.test › o que já foi digitado fica` |
| F10-04 | algum campo do endereço já preenchido (inclusive pelo CEP) | a consulta responde | o endereço inteiro fica como está: não mistura endereço digitado com o da Receita | `clientes.test › o que já foi digitado fica` |
| F10-05 | o endereço veio do CNPJ | a pessoa não mexe no CEP | o ViaCEP não é chamado de novo para esse CEP e não sobrescreve o que veio da Receita (exceção: o F10-29, quando só o CEP vem) | `clientes.test › CNPJ completo preenche…` (ViaCEP não chamado) |
| F10-06 | a consulta preencheu campos | a pessoa olha o formulário | uma linha discreta diz o que veio da Receita ("Preenchido com os dados da Receita; confira antes de salvar. O telefone pode ser o do escritório de contabilidade.", ajustado em 05/11: o e-mail não vem) | `clientes.test › CNPJ completo preenche…` |
| F10-07 | um CNPJ completo | a consulta está em andamento | o campo mostra que está buscando, como o CEP | `clientes.test › mostra que está buscando, e só a resposta do CNPJ atual vale` |
| F10-08 | um CNPJ sendo consultado | a pessoa troca o CNPJ antes da resposta | só a resposta do CNPJ atual é aplicada | `clientes.test › mostra que está buscando, e só a resposta do CNPJ atual vale` |
| F10-09 | um CNPJ sendo consultado | a pessoa fecha a janela | nada é aplicado e nenhum erro aparece | `clientes.test › fechar a janela durante a consulta abandona tudo` |
| F10-10 | edição de um cliente que já tem CNPJ | o formulário abre | nada é consultado (decisão 3) | `clientes.test › na edição, o CNPJ salvo não é consultado` |
| F10-11 | edição de um cliente | a pessoa troca o CNPJ por outro válido | consulta e preenche só os campos vazios, como no F10-03 e F10-04 | `clientes.test › na edição, o CNPJ salvo não é consultado` |
| F10-29 | CNPJ de empresário individual (MEI, por exemplo) e endereço vazio | a consulta responde | a BrasilAPI não traz logradouro, número, complemento nem telefone (privacidade do Minha Receita): CEP, bairro, cidade e UF da Receita entram, e o ViaCEP completa só o que ficou vazio (logradouro, bairro) e a grafia da cidade se ninguém mexeu nela, sem tirar o foco do CNPJ; a resposta do ViaCEP vale só para o CNPJ atual e com a janela aberta (05/11) | `clientes.test › empresário individual: …` (4 testes) |
| F10-30 | formulário de cliente | abre | CPF ou CNPJ é o primeiro campo, com o foco; no cliente novo, diz que o CNPJ busca os dados na Receita (pedido do Daniel, 05/11) | `clientes.test › o CNPJ é o primeiro campo e já abre com o foco` |

### Situação cadastral (F10a)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F10-12 | CNPJ de empresa baixada, inapta, suspensa ou nula | a consulta responde | preenche como no F10-01 e mostra um aviso perto do CNPJ, com a situação e a data (decisão 4) | `clientes.test › empresa baixada: avisa sem bloquear` |
| F10-13 | o aviso de situação na tela | a pessoa salva | o cliente é salvo normalmente: o aviso não bloqueia | `clientes.test › empresa baixada: avisa sem bloquear` |
| F10-14 | o aviso de situação na tela | a pessoa troca o CNPJ ou o apaga | o aviso some | `clientes.test › empresa baixada: avisa sem bloquear` |

### Falhas (F10a)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F10-15 | CNPJ válido sem cadastro no provedor | a consulta responde 404 | "Não encontramos este CNPJ na Receita. Preencha à mão." Nada é preenchido; salvar funciona | `cnpj.test › 404 é CNPJ sem cadastro`; `clientes.test › sem cadastro` |
| F10-16 | provedor com erro 5xx, limite atingido (429), outro 4xx (ex.: um CNPJ alfanumérico que ele ainda não conhece) ou CORS recusado (`TypeError` no fetch) | a pessoa completa o CNPJ | "Não deu para buscar os dados agora. Preencha à mão." Nada é preenchido; salvar funciona | `cnpj.test › fora do ar, limite, outro 4xx, CORS`; `clientes.test › provedor com erro` |
| F10-17 | provedor lento | passa o tempo máximo (4 s, como o do ViaCEP) | a consulta é abandonada com a mesma mensagem do F10-16 | `cnpj.test › espera no máximo 4 s`, `tempo esgotado` |
| F10-18 | resposta fora do formato esperado (JSON estranho, campos ausentes) | a consulta responde | tratada como o F10-16, sem quebrar a tela | `cnpj.test › JSON estranho`, `sem razão social`, `não é JSON` |
| F10-19 | o campo CPF ou CNPJ | a pessoa completa um CPF válido | nenhuma chamada sai do navegador; nada é preenchido | `clientes.test › CPF, CNPJ incompleto ou com dígito errado não consultam` |

### Privacidade e CSP (F10a)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F10-20 | uma consulta de CNPJ | ela sai do navegador | `credentials: 'omit'`, `referrerPolicy: 'no-referrer'` e só o CNPJ na URL, como no ViaCEP | `cnpj.test › traduz a resposta, sem cookie nem Referer` |
| F10-21 | o build do SPA | roda `pnpm csp:conferir` | o `connect-src` tem a origem do provedor (e a da reserva, se a decisão 1 for b) e a do ViaCEP, sem curinga; o `apps/web/scripts/conferir-csp.mjs` passa a conferir o `connect-src`, que hoje não confere | `pnpm csp:conferir` (CI, job Web) |

### Validação e CNPJ alfanumérico (F10b)

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F10-22 | CPF ou CNPJ com dígito verificador errado | a pessoa sai do campo ou tenta salvar | erro no campo ("CPF ou CNPJ inválido."), sem consulta e sem ida à API (decisão 5) | `clientes.test › documento com dígito errado mostra o erro no campo e não salva`; `documento.test` |
| F10-23 | documento inválido por chamada direta | chega à API | `400` em `errors.documento` (regressão) | `ClientesTests › Criar_DocumentoInvalido_400NoCampo` (já existe) |
| F10-24 | o campo CPF ou CNPJ | a pessoa digita uma letra | a máscara aceita letra só nas 12 primeiras posições do CNPJ; CPF continua só numérico | `mascaras.test › documento 12abc34501de35`; `clientes.test › aceita CNPJ alfanumérico` |
| F10-25 | um CNPJ alfanumérico | a validação roda (front e API) | os dígitos verificadores são conferidos pelo código ASCII menos 48, com o exemplo oficial da Receita como caso de teste | `ClienteTests › DocumentoValido_GuardaSemPontuacaoEmMaiusculas`; `documento.test` |
| F10-26 | um CNPJ alfanumérico em minúsculas | a API grava | guarda em maiúsculas, sem pontuação; não precisa de migration (coluna de 14 caracteres) | `ClientesTests › Criar_CnpjAlfanumerico_GuardaEmMaiusculas_EAchaNaBusca` |
| F10-27 | cliente com CNPJ alfanumérico | a lista e a ficha mostram o documento | formatado com as letras (`12.ABC.345/01DE-35`), sem perder caracteres | `clientes.test › lista e ficha mostram o CNPJ alfanumérico formatado` |
| F10-28 | cliente com CNPJ alfanumérico | a busca da lista recebe um trecho dele, com ou sem pontuação | o cliente é encontrado (hoje a busca usa só os dígitos) | `ClientesTests › Criar_CnpjAlfanumerico_GuardaEmMaiusculas_EAchaNaBusca` |

Permissão e isolamento: sem cenário novo. Não há rota nova; o formulário segue exigindo `cliente.gerenciar` (testes de clientes existentes), e a consulta não toca a API do Acordu nem dados de outra empresa.

## 3. Fora de escopo

- Consulta de CPF (RF-CLI-08: dado pessoal, sem API pública).
- Inscrição estadual (continua manual; não vem nos dados abertos da Receita).
- Guardar a situação cadastral ou a data da consulta no cliente, ou mostrar na ficha (decisão 4).
- Consulta pela API do Acordu (segurança §9: no nível 0, do navegador).
- Atualizar em lote clientes já cadastrados.
- Sócios, CNAE, porte e regime tributário: o Acordu não tem onde guardar.

## 4. Decisões que dependem do Daniel

**Antes das opções, o que deu para verificar (04/11):** a rede deste ambiente bloqueia os três provedores, tanto pelo `curl` quanto pelo Chromium do Playwright (`CONNECT tunnel failed, 403` no proxy). Por isso não consegui conferir aqui se respondem nem se mandam `Access-Control-Allow-Origin`. O que está abaixo vem da documentação pública. **A primeira tarefa do F10a é abrir uma página de outra origem num navegador comum e chamar o provedor escolhido**; se o CORS não vier liberado, a decisão 1 volta para você antes de qualquer código.

| Provedor | Limite grátis | CORS do navegador | Campos úteis |
|---|---|---|---|
| BrasilAPI (`brasilapi.com.br/api/cnpj/v1/{cnpj}`) | sem número publicado; os termos pedem uso no ritmo de uma pessoa e proíbem varredura | não documentado; é um projeto pensado para front (o README cita a falta de CORS dos Correios como motivo de existir) | `razao_social`, `nome_fantasia`, `descricao_situacao_cadastral`, `data_situacao_cadastral`, `cep`, `descricao_tipo_de_logradouro`, `logradouro`, `numero`, `complemento`, `bairro`, `municipio`, `uf`, `ddd_telefone_1`; `email` existe no formato, mas vem sempre nulo (o Minha Receita o apaga por privacidade, conferido em 05/11) |
| CNPJá aberta (`open.cnpja.com/office/{cnpj}`) | 5 consultas por minuto por IP | não documentado | razão social, fantasia, situação, endereço, telefones e e-mails (lista na referência deles) |
| ReceitaWS pública (`receitaws.com.br/v1/cnpj/{cnpj}`) | 3 por minuto por IP (a CNPJ.ws, parecida, também é 3/min e bloqueia o IP por uma hora com muitos 429) | não documentado | `nome`, `fantasia`, `situacao`, endereço, `telefone`, `email` |

Como a consulta sai do navegador, o limite por IP é o da rede de quem cadastra, não o de todos os usuários somados (segurança §9). 3 a 5 por minuto bastam para quem cadastra clientes à mão.

1. **Provedor principal e reserva.**
   - (a) **Só a BrasilAPI**, e a falha cai no "preencha à mão" (F10-16) — **recomendado**. Gratuita, sem cadastro, sem limite numérico baixo e de dados abertos da Receita. Uma reserva dobraria origem na CSP, mapeamento e um terceiro a mais recebendo o CNPJ, para cobrir um caso que o RF-CLI-08 já aceita (falha não impede o cadastro).
   - (b) BrasilAPI e, se ela falhar (5xx, 429 ou tempo), a CNPJá aberta. Cobre a queda de um provedor, ao custo acima, e soma até 8 s de espera no pior caso (4 s + 4 s).
   - (c) CNPJá ou ReceitaWS como principal. Limite baixo e com cara de produto comercial; só se a BrasilAPI não liberar CORS.
   - E-mail: se a BrasilAPI de fato não trouxer e-mail, o RF-CLI-08 ("se vazios, e-mail e telefone") se cumpre com o que vier: o e-mail fica vazio, e isso não é motivo para a reserva. A conferência do início do F10a diz quais campos vêm; se faltar algo além do e-mail, a decisão volta para você.

2. **Campos e mapeamento.**
   - Razão social → `razaoSocial`. Endereço como bloco: CEP, logradouro (tipo + nome, ex.: "Rua das Flores"), número ("S/N" vira vazio), complemento, bairro, cidade, UF; só se o endereço estiver todo vazio (F10-04).
   - Nome sem nome fantasia: (a) **nome = razão social** — **recomendado**: o nome é obrigatório, e a razão social é o melhor ponto de partida para a pessoa encurtar; (b) deixar o nome vazio para a pessoa escrever.
   - Telefone e e-mail da Receita, que costumam ser do contador: (a) **preencher se vazios, como diz o RF-CLI-08, e marcar na linha do F10-06 que podem ser do escritório de contabilidade** — **recomendado**: o e-mail geral do cliente não recebe avisos do Acordu (quem recebe são os contatos), então o risco de mandar algo ao contador é baixo, e a pessoa vê o que veio; (b) não preencher e ajustar o RF-CLI-08 da ERS.

3. **Quando consultar.**
   - (a) **Automático ao completar um CNPJ válido diferente do que o cliente já tinha salvo**, como o CEP; na edição, abrir o formulário não consulta, e trocar o CNPJ consulta e preenche só os vazios (F10-10, F10-11) — **recomendado**: é o comportamento que o formulário já tem com o CEP, sem botão novo.
   - (b) Só por botão "Buscar dados" ao lado do CNPJ. Mais controle, um clique a mais em todo cadastro.
   - (c) Os dois: automático no cliente novo e botão para reconsultar na edição (atualizar endereço de quem mudou). Mais completo; o botão só faria sentido sobrescrevendo, o que o RF-CLI-08 não permite.

4. **Aviso de situação cadastral.**
   - (a) **Aviso âmbar logo abaixo do CNPJ, só no formulário**: "Na Receita, este CNPJ está BAIXADA desde 12/03/2024. Dá para salvar mesmo assim." — **recomendado**: atende o RF-CLI-08 (aviso sem bloquear) sem dado novo no cliente.
   - (b) Também guardar a situação e mostrar na ficha. Pede campo novo no domínio, migration e a pergunta de quando atualizar; fica para depois, se fizer falta.

5. **Validação dos dígitos.**
   - (a) **No front (erro no campo ao sair dele e ao salvar, e sem consulta com dígito errado) e na API, que já valida hoje** (`Documento.Normalizar`, `400` em `documento`) — **recomendado**: o front só antecipa o erro, e a API continua sendo a regra. A validação do front entra no F10b, junto do alfanumérico, para não ser escrita duas vezes; até lá, o F10a só consulta CNPJ de 14 dígitos com dígitos verificadores certos.
   - (b) Só na API, como hoje: o erro aparece só ao salvar, e o front precisaria decidir sozinho quando não consultar.

6. **CNPJ alfanumérico.** A Instrução Normativa RFB nº 2.229/2024 permite à Receita emitir, desde julho de 2026, CNPJ com letras nas 12 primeiras posições; os existentes não mudam, e os dois dígitos verificadores continuam numéricos, calculados com o código ASCII de cada caractere menos 48 (letras valem de 17 a 42). Isso não é hipótese: hoje (04/11) já pode existir empresa que o Acordu não cadastra, porque o `Documento` da API, a máscara do front (`digitos()` em `mascaras.ts`, usada também no cursor do `CampoMascarado`), a exibição na lista e na ficha e a busca da lista (`ConsultasCliente`, que filtra só dígitos) aceitam só números. É defeito do RF-CLI-01, não parte do RF-CLI-08.
   - (a) **Corrigir agora, no F10b, que sai primeiro**: não depende do provedor nem da conferência de CORS, e leva junto a validação de dígitos do front (decisão 5) e o ajuste do RF-CLI-01 na ERS — **recomendado**.
   - (b) Deixar para um marco próprio. Quem tiver um cliente com CNPJ alfanumérico não consegue cadastrar o documento até lá.

## 5. Impacto nos documentos

- **ERS:** RF-CLI-01 ganha "CNPJ numérico ou alfanumérico (IN RFB 2.229/2024)", no F10b (decisão 6a). RF-CLI-08 só muda se a decisão 2 de telefone e e-mail for (b).
- **Segurança §9:** o nome do provedor escolhido no item da consulta de CNPJ e na política completa do SPA (`connect-src`).
- **Convenções §4:** a regra das consultas a serviços públicos do navegador (tempo máximo, sem cookie e sem Referer, falha vira preenchimento à mão), que hoje está só no comentário do `viaCep.ts`.
- **Contrato da API:** §4 (cliente), formato do `documento` se a decisão 6 for (a). Sem rota nova.
- **Modelo de domínio:** `Documento` aceita CNPJ alfanumérico, normalizado em maiúsculas (decisão 6a); CPF continua só numérico. Sem migration.
- **Read side:** a busca de clientes por documento passa a considerar letras (decisão 6a).
- **ADR:** nenhum.

## 6. Divisão em PRs

Cada PR contra a `main`, sem empilhar, com testes e CI verde.

| PR | Entrega | Cenários |
|---|---|---|
| F10b | API e front: CNPJ alfanumérico no `Documento` (maiúsculas), na busca da lista, na máscara e no cursor do `CampoMascarado`, na exibição da lista e da ficha; validação dos dígitos no front; ERS (RF-CLI-01), contrato §4, domínio e read side | F10-22 a F10-28 |
| F10a | Front: conferência de CORS e dos campos do provedor (primeiro passo), `shared/api/cnpj.ts` no padrão do `viaCep.ts`, preenchimento no `JanelaCliente` (nome, razão social, documento, e-mail e telefone passam a campos controlados), avisos, CSP e `csp:conferir` do `connect-src`, convenções §4 e segurança §9 | F10-01 a F10-21 |
| ajuste de 05/11 | Front: modo de privacidade da BrasilAPI (empresário individual, e-mail), CNPJ como primeiro campo | F10-29, F10-30 |

Ordem: F10b primeiro (corrige um defeito que já existe e não depende de nada); o F10a parte da máscara e da validação dele.

## 7. Registro

- 04/11 — rascunho. Requisito do PR #139. Provedores não verificados daqui (rede bloqueada); CORS conferido no início do F10a. Revisão: decisão 6 com a fonte (IN RFB 2.229/2024) e o alcance real no código, F10b antes do F10a com a validação do front, cenários de 4xx, CORS, ViaCEP, fechamento da janela e "buscando".
- 04/11 — F10b implementado (CNPJ alfanumérico na API e no front, validação no front). O read side não descreve a busca de clientes; a regra nova ficou no contrato §4. Revisão: erro também ao sair do campo, rótulo colado ("CNPJ: …") ignorado na máscara, busca só compara o documento quando o termo tem dígito, teste de tela da lista e da ficha, nota em Novidades.
- 04/11 — F10a. CORS da BrasilAPI conferido no navegador do Daniel (resposta com `razao_social`) e no código dela (`origin: '*'`); os campos vêm do Minha Receita, inclusive `email`. A Receita manda tudo em maiúsculas e sem acento: razão social fica como vem (nome oficial); nome, logradouro (tipo + nome), complemento, bairro e cidade vão em forma de título com as preposições em minúsculas, sem recuperar acentos (opção recomendada, assumida sem resposta do Daniel). Aviso de situação abaixo da linha do documento e da inscrição estadual, ocupando a largura.
- 04/11 — revisão do F10a: siglas de tipo de empresa (ME, EPP, S/A…) e números romanos ficam em maiúsculas, hífen e apóstrofo começam palavra, "e" no fim fica maiúsculo; a frase do F10-06 só aparece se algum campo foi preenchido; e-mail fora do formato vem vazio; ajuda da tela de clientes atualizada.
- 05/11 — teste do Daniel com um CNPJ de empresário individual: só vieram razão social e nome. O Minha Receita, fonte da BrasilAPI, publica em modo de privacidade: o e-mail nunca vem, e para natureza jurídica "individual" também somem logradouro, número, complemento e telefones (ficam CEP, bairro, cidade e UF). O registro de 04/11 estava errado ao dizer que o e-mail vinha. Ajustes: F10-29 (CEP, bairro, cidade e UF da Receita, e o ViaCEP completa a rua), F10-30 (CNPJ como primeiro campo), frase do F10-06 sem e-mail.
