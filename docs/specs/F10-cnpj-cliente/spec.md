# Spec — F10: preenchimento do cliente pelo CNPJ

| | |
|---|---|
| **Status** | Rascunho |
| **Data** | 04/11/2026 |
| **Requisitos** | RF-CLI-08 (decidido em 04/11), RF-CLI-01 |
| **ADRs** | 0010 (sem SDK de provedor), 0011 |

## 1. Objetivo

Ao digitar o CNPJ no cadastro do cliente, o formulário busca os dados abertos da Receita e preenche o que estiver vazio, avisando quando a empresa não está ativa. A consulta sai do navegador, como a do CEP (segurança §9), e falha nunca impede o cadastro.

## 2. Cenários de aceite

"Consultar" = a chamada do navegador ao provedor escolhido na decisão 1, sem cookie e sem Referer, só com o CNPJ na URL. Os textos entre aspas são propostas; os nomes de campo são os do formulário de hoje (`JanelaCliente`).

### Consulta e preenchimento

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F10-01 | formulário de cliente novo, vazio | a pessoa completa um CNPJ válido de empresa ativa com nome fantasia | preenche razão social, nome (= nome fantasia) e o endereço (CEP, logradouro, número, complemento, bairro, cidade, UF), e e-mail e telefone conforme a decisão 2 | — |
| F10-02 | formulário de cliente novo, vazio | o CNPJ é de empresa sem nome fantasia | o nome recebe a razão social (decisão 2) | — |
| F10-03 | nome, razão social, e-mail ou telefone já digitados | a consulta responde | o que já foi digitado fica como está; só os campos vazios recebem dados | — |
| F10-04 | algum campo do endereço já preenchido (inclusive pelo CEP) | a consulta responde | o endereço inteiro fica como está: não mistura endereço digitado com o da Receita | — |
| F10-05 | a consulta preencheu campos | a pessoa olha o formulário | uma linha discreta diz o que veio da Receita ("Preenchido com os dados da Receita. Confira antes de salvar.") | — |
| F10-06 | um CNPJ sendo consultado | a pessoa troca o CNPJ antes da resposta | só a resposta do CNPJ atual é aplicada | — |
| F10-07 | edição de um cliente que já tem CNPJ | o formulário abre | nada é consultado (decisão 3) | — |
| F10-08 | edição de um cliente | a pessoa troca o CNPJ por outro válido | consulta e preenche só os campos vazios, como no F10-03 e F10-04 | — |

### Situação cadastral

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F10-09 | CNPJ de empresa baixada, inapta, suspensa ou nula | a consulta responde | preenche como no F10-01 e mostra um aviso perto do CNPJ, com a situação e a data (decisão 4) | — |
| F10-10 | o aviso de situação na tela | a pessoa salva | o cliente é salvo normalmente: o aviso não bloqueia | — |
| F10-11 | o aviso de situação na tela | a pessoa troca o CNPJ ou o apaga | o aviso some | — |

### Falhas

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F10-12 | CNPJ com dígitos válidos mas sem cadastro na Receita | a consulta responde 404 | "Não encontramos este CNPJ na Receita. Preencha à mão." Nada é preenchido; salvar funciona | — |
| F10-13 | provedor fora do ar, com erro 5xx ou limite atingido (429) | a pessoa completa o CNPJ | "Não deu para buscar os dados agora. Preencha à mão." Nada é preenchido; salvar funciona | — |
| F10-14 | provedor lento | passa o tempo máximo (4 s, como o do ViaCEP) | a consulta é abandonada com a mesma mensagem do F10-13 | — |
| F10-15 | resposta fora do formato esperado (JSON estranho, campos ausentes) | a consulta responde | tratada como o F10-13, sem quebrar a tela | — |

### CPF e validação

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F10-16 | o campo CPF ou CNPJ | a pessoa completa um CPF válido | nenhuma chamada sai do navegador; nada é preenchido | — |
| F10-17 | CPF ou CNPJ com dígito verificador errado | a pessoa sai do campo ou tenta salvar | erro no campo ("CPF ou CNPJ inválido."), sem consulta e sem ida à API (decisão 5) | — |
| F10-18 | um documento inválido que passou pelo front (chamada direta) | chega à API | `400` em `errors.documento`, como já é hoje (`Documento.Normalizar`) | — |
| F10-19 | CNPJ alfanumérico válido (formato da Receita desde julho de 2026) | a pessoa digita | o campo aceita letras nas 12 primeiras posições, valida os dígitos verificadores e a API grava (decisão 6) | — |

### Privacidade e CSP

| Id | Dado | Quando | Então | Teste |
|---|---|---|---|---|
| F10-20 | uma consulta de CNPJ | ela sai do navegador | `credentials: 'omit'`, `referrerPolicy: 'no-referrer'` e só o CNPJ na URL, como no ViaCEP | — |
| F10-21 | o build do SPA | roda `pnpm csp:conferir` | a origem do provedor está no `connect-src` (e a da reserva, se a decisão 1 for b), sem curinga | — |

Permissão e isolamento: o formulário só abre para quem tem `cliente.gerenciar` (sem mudança), e a consulta não toca a API do Acordu nem dados de outra empresa.

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
| BrasilAPI (`brasilapi.com.br/api/cnpj/v1/{cnpj}`) | sem número publicado; os termos pedem uso no ritmo de uma pessoa e proíbem varredura | não documentado; é um projeto pensado para front (o README cita a falta de CORS dos Correios como motivo de existir) | `razao_social`, `nome_fantasia`, `descricao_situacao_cadastral`, `data_situacao_cadastral`, `cep`, `descricao_tipo_logradouro`, `logradouro`, `numero`, `complemento`, `bairro`, `municipio`, `uf`, `ddd_telefone_1`; e-mail não aparece na tipagem da biblioteca Rust consultada (confirmar) |
| CNPJá aberta (`open.cnpja.com/office/{cnpj}`) | 5 consultas por minuto por IP | não documentado | razão social, fantasia, situação, endereço, telefones e e-mails (lista na referência deles) |
| ReceitaWS pública (`receitaws.com.br/v1/cnpj/{cnpj}`) | 3 por minuto por IP (a CNPJ.ws, parecida, também é 3/min e bloqueia o IP por uma hora com muitos 429) | não documentado | `nome`, `fantasia`, `situacao`, endereço, `telefone`, `email` |

Como a consulta sai do navegador, o limite por IP é o da rede de quem cadastra, não o de todos os usuários somados (segurança §9). 3 a 5 por minuto bastam para quem cadastra clientes à mão.

1. **Provedor principal e reserva.**
   - (a) **Só a BrasilAPI**, e a falha cai no "preencha à mão" (F10-13) — **recomendado**. Gratuita, sem cadastro, sem limite numérico baixo e de dados abertos da Receita. Uma reserva dobraria origem na CSP, mapeamento e um terceiro a mais recebendo o CNPJ, para cobrir um caso que o RF-CLI-08 já aceita (falha não impede o cadastro).
   - (b) BrasilAPI e, se ela falhar (5xx, 429 ou tempo), a CNPJá aberta. Cobre a queda de um provedor, ao custo acima, e soma até 8 s de espera no pior caso (4 s + 4 s).
   - (c) CNPJá ou ReceitaWS como principal. Limite baixo e com cara de produto comercial; só se a BrasilAPI não liberar CORS.

2. **Campos e mapeamento.**
   - Razão social → `razaoSocial`. Endereço como bloco: CEP, logradouro (tipo + nome, ex.: "Rua das Flores"), número ("S/N" vira vazio), complemento, bairro, cidade, UF; só se o endereço estiver todo vazio (F10-04).
   - Nome sem nome fantasia: (a) **nome = razão social** — **recomendado**: o nome é obrigatório, e a razão social é o melhor ponto de partida para a pessoa encurtar; (b) deixar o nome vazio para a pessoa escrever.
   - Telefone e e-mail da Receita, que costumam ser do contador: (a) **preencher se vazios, como diz o RF-CLI-08, e marcar na linha do F10-05 que podem ser do escritório de contabilidade** — **recomendado**: o e-mail geral do cliente não recebe avisos do Acordu (quem recebe são os contatos), então o risco de mandar algo ao contador é baixo, e a pessoa vê o que veio; (b) não preencher e ajustar o RF-CLI-08 da ERS.

3. **Quando consultar.**
   - (a) **Automático ao completar um CNPJ válido diferente do que o cliente já tinha salvo**, como o CEP; na edição, abrir o formulário não consulta, e trocar o CNPJ consulta e preenche só os vazios (F10-07, F10-08) — **recomendado**: é o comportamento que o formulário já tem com o CEP, sem botão novo.
   - (b) Só por botão "Buscar dados" ao lado do CNPJ. Mais controle, um clique a mais em todo cadastro.
   - (c) Os dois: automático no cliente novo e botão para reconsultar na edição (atualizar endereço de quem mudou). Mais completo; o botão só faria sentido sobrescrevendo, o que o RF-CLI-08 não permite.

4. **Aviso de situação cadastral.**
   - (a) **Aviso âmbar logo abaixo do CNPJ, só no formulário**: "Na Receita, este CNPJ está BAIXADA desde 12/03/2024. Dá para salvar mesmo assim." — **recomendado**: atende o RF-CLI-08 (aviso sem bloquear) sem dado novo no cliente.
   - (b) Também guardar a situação e mostrar na ficha. Pede campo novo no domínio, migration e a pergunta de quando atualizar; fica para depois, se fizer falta.

5. **Validação dos dígitos.**
   - (a) **No front (erro no campo ao sair dele e ao salvar, e sem consulta com dígito errado) e na API, que já valida hoje** (`Documento.Normalizar`, `400` em `documento`) — **recomendado**: o front só antecipa o erro, e a API continua sendo a regra.
   - (b) Só na API, como hoje: o erro aparece só ao salvar, e o front precisaria decidir sozinho quando não consultar.

6. **CNPJ alfanumérico** (achado ao preparar a spec: a Receita pode emitir CNPJ com letras nas 12 primeiras posições desde julho de 2026; os existentes não mudam, e os dígitos verificadores usam o código ASCII menos 48). Hoje a máscara do front e o `Documento` da API só aceitam dígitos, então uma empresa nova com CNPJ alfanumérico não pode ser cadastrada.
   - (a) **Aceitar no F10, num PR próprio da API e da máscara do front (F10b)** — **recomendado**: é correção de um requisito que já existe (RF-CLI-01), e o F10 mexe no mesmo campo. A consulta usa o mesmo caminho; se o provedor ainda não tiver o CNPJ, cai no F10-12.
   - (b) Deixar para um marco próprio. Fica o risco de um cliente real não conseguir cadastrar o CNPJ.

## 5. Impacto nos documentos

- **ERS:** RF-CLI-08, se a decisão 2 de telefone e e-mail for (b); RF-CLI-01 ganha "CNPJ numérico ou alfanumérico" se a decisão 6 for (a).
- **Segurança §9:** o nome do provedor escolhido no item da consulta de CNPJ e na política completa do SPA (`connect-src`).
- **Convenções §4:** a regra das consultas a serviços públicos do navegador (tempo máximo, sem cookie e sem Referer, falha vira preenchimento à mão), que hoje está só no comentário do `viaCep.ts`.
- **Contrato da API:** §4 (cliente), formato do `documento` se a decisão 6 for (a). Sem rota nova.
- **Modelo de domínio:** `Documento` aceita CNPJ alfanumérico, se a decisão 6 for (a).
- **ADR:** nenhum.

## 6. Divisão em PRs

Cada PR contra a `main`, sem empilhar, com testes e CI verde.

| PR | Entrega | Cenários |
|---|---|---|
| F10a | Front: conferência de CORS do provedor (primeiro passo), `shared/api/cnpj.ts` no padrão do `viaCep.ts`, preenchimento no `JanelaCliente`, avisos, validação dos dígitos no front, CSP e `csp:conferir`, convenções §4 e segurança §9 | F10-01 a F10-17, F10-20, F10-21 |
| F10b | API e front: CNPJ alfanumérico no `Documento` e na máscara, com os testes de validação; contrato e domínio (só com a decisão 6a) | F10-18, F10-19 |

F10a e F10b são independentes; se os dois mexerem na máscara, o segundo a entrar se ajusta ao primeiro.

## 7. Registro

- 04/11 — rascunho. Requisito do PR #139. Provedores não verificados daqui (rede bloqueada); CORS conferido no início do F10a.
