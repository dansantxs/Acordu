# Notas do analista (F14, etapa 2)

Para o arquiteto, sem decisão tomada aqui.

- O que já existe e a spec reaproveita: `Tenant` (sem situação nem documento), `ExpurgoCadastros` no diário (F9d, classificação das entidades, transação serializável, contas com rastro em outra empresa), Turnstile e limite por IP no registro (F9c), `Documento` no Domain (CPF/CNPJ com CNPJ alfanumérico, usado no Cliente), convites da equipe e do portal (token só em hash, 7 dias, uso único), empresa de demonstração do bot das capturas (`apps/web/ajuda/demonstracao.ts`, cria pela API).
- Não existe: área de operação da Nebrys nem conta de operador (o F13 deixou a tela da Nebrys fora), landing pública, páginas de termos e privacidade, lista de espera.
- Documentos que hoje contradizem o F14 e vão precisar mudar no PR de cada parte: segurança §4 ("Fora, por ora: lista de domínios descartáveis e período de teste atrelado a CNPJ"), segurança §6 (a política de privacidade já é citada para a transferência internacional), F9 §3 (fora de escopo do mesmo tema), contrato §2 (corpo do registro), deploy-ci §5 (passos novos do diário), modelo de domínio §4.11 (Tenant).
- Retenção dos backups (14 dias, deploy-ci §8) entra no texto da política (F14-85): a empresa expurgada continua nos backups até 14 dias.
- RF-AUT-07 e o expurgo novo (RF-EMP-03) convivem: empresa em TESTE não liberada e sem dados continua caindo no do F9 em 7 dias.
- Devolução do /plano (13/11): cenários F14-97 a F14-111 acrescentados sem renumerar. Os dependentes de pergunta (F14-104, F14-105, F14-107, F14-108, F14-110, F14-111) saem no G2 se a recomendação não for aceita. As perguntas 16 a 19 da spec são os itens 1 a 4 do design §1.1, trazidos para a spec por serem de requisito; a pergunta 15 é a concorrência das ações da Nebrys. Se a pergunta 16 for aceita, a RF-AUT-09 da ERS ganha a exceção do cadastro abandonado (não mudei antes do G2).
- Sugestão ao arquiteto: o design §1.1 pode passar a apontar para as perguntas 16 a 19 da spec, e a §3 precisa incluir F14-97 a F14-100 (F14b), F14-111 (F14c), F14-101 a F14-103 (F14i), F14-104 (F14h), F14-105 (F14l), F14-106 a F14-108 (F14j), F14-109 (F14n), F14-110 (F14p).
- Permissão para dados de exemplo (F14-95) ficou `configuracao.gerenciar` por ser a do Dono para a empresa; ajustar se o design preferir outra.
- G2 registrado (13/11, PR #186): a §4 da spec virou "Decisões do G2", com as opções preservadas. Divergiram da recomendação: prazo até o expurgo de 30 dias (não 60) e empresas anteriores ao F14 apagadas (CANCELADA pela migration do F14a com data antiga, expurgo no primeiro diário com o F14i; F14-08, F14-112, F14-113). Canal: contato@acordu.app.br. Marcas "dependente da pergunta" retiradas. ERS sem referências pendentes à §4; RF-AUT-09 com a exceção do cadastro abandonado.
- Para o arquiteto: o design §3 ainda diz "migration com as existentes em ATIVA" no F14a e não lista F14-112 (F14i) e F14-113 (F14j); o padrão de 60 dias de `Empresas__DiasAteExpurgo` passa a 30.

## 18/11 — aceite atrás dos textos publicados (F14h)

Decisão do Daniel (18/11): "Pôr o aceite atrás da TEXTOS_LEGAIS_PUBLICADOS". Origem: `notas/desenvolvedor-F14f-esconder.md`, seção "Para o F14h", e design §4 (ordem do corpo do registro).

- Spec: termo "textos publicados" no bloco dos textos legais; F14-79 a F14-83 ganharam "textos publicados;" no dado, sem mudar o resto; novos F14-114 a F14-121 (estado desligado, virada para ligado, portal, mesmo valor no front e na API). Sem renumerar.
- ERS: RF-AUT-11 ganhou a condição (desligada: sem caixa, sem links, sem aceite registrado; ligada: quem não tem aceite recebe o "ciente").
- F14-119 (conta sem aceite nenhum quando os textos são publicados): escolhi o mesmo tratamento da versão nova (aviso com link até o "ciente", sem bloquear), por coerência com a pergunta 12. Opção não escolhida: pedir a caixa de aceite (não o "ciente") a quem nunca aceitou, ainda sem bloquear. Diferença jurídica pequena e o advogado pode reabrir junto da #194; se o Daniel preferir, só o "então" do F14-119 muda.
- F14-116 (registro com aceite e textos não publicados): ignorar e não gravar, em vez de `400`, para um front desatualizado não travar o cadastro. Coerente com "não grava aceite enquanto desligada".
- F14-118: o "ciente" direto é recusado; o tipo de erro fica para o design, como os demais.
- F14-121: o nome técnico e como a API recebe o valor ficam para o design (o front hoje lê `VITE_TEXTOS_LEGAIS_PUBLICADOS`, que vem da variável opcional do deploy). O cenário só exige que os dois nunca divirjam.
- Devolução do /plano (18/11): o F14-121 prometia que a divergência nunca existe, o que contradizia a janela da virada do D16 (a API sobe antes do SPA; nesses minutos, e numa aba antiga até recarregar, o cadastro recebe `400` no aceite). O cenário agora vale para o deploy concluído, com a tela servida por ele, e a janela fica fora dele ("fora da janela da virada, ver design"). A decisão de aceitar o risco é do G2, no D16; não a escrevi na spec. Nenhum outro cenário mudou.
- F14-84 e F14-109 (links sem aceite) já ficaram escondidos pelo F14f sem mudar os cenários; não mexi neles. Para o arquiteto: o design §3 do F14h passa a incluir F14-114 a F14-121, e o §4 (linha 274) deixa de ter a alternativa em aberto.
