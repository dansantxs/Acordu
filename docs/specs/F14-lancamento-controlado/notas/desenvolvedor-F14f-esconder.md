# Notas do desenvolvedor (F14f, esconder os textos legais)

Trabalho avulso do F14 (issue #182), ligado à issue #194. Pedido do Daniel (16/11): "pode esconder as páginas e os links atrás de uma configuração". Os textos do F14f têm marcadores `[a preencher pelo Daniel]` e `[a confirmar…]` que só se resolvem depois do registro da empresa (#194); até lá, um deploy não pode mostrar as páginas nem os links. Nenhum cenário muda; sem mudança na API.

## Premissas

- **Configuração:** `VITE_TEXTOS_LEGAIS_PUBLICADOS`, lida só em `shared/config/ambiente.ts` (`TEXTOS_LEGAIS_PUBLICADOS`) e tipada em `vite-env.d.ts`. Liga só com `true` (sem diferenciar maiúsculas, aparado); ausente, vazia, `false`, `1` ou qualquer outro valor deixam escondido (padrão seguro). No deploy, vem da variável opcional `TEXTOS_LEGAIS_PUBLICADOS` do GitHub: sem ela o build segue e os textos ficam escondidos. Não entrou na conferência do passo "Variáveis da API" de propósito (não é obrigatória).
- **Links:** `LinksLegais` devolve `null` quando desligada. Todas as telas que citam os textos passam por ele (`CapaAcesso` e `CascaPortal`), então um ponto só esconde tudo.
- **Páginas:** `/termos` e `/privacidade` têm `beforeLoad: exigirTextosLegaisPublicados` (`features/legal/publicacao.ts`), que lança `notFound()` do TanStack Router; cai no `notFoundComponent` da raiz ("Página não encontrada."), o mesmo de um endereço que não existe fora do app. Não chama a API nem espera a sessão. A URL fica a pedida (não redireciona).
- **O texto continua no bundle:** o Markdown entra no chunk da rota (`?raw`), que o build gera mesmo escondido; quem procurar nos arquivos estáticos acha o rascunho. Não há link nem página para ele. Tirar do bundle exigiria condicionar o import no build (fora do padrão "lida só em `ambiente.ts`"); não fiz por ser rascunho sem dado pessoal. Se o Daniel quiser, é a melhoria seguinte.
- **Testes:** o `vite.config.ts` liga a configuração nos testes (`VITE_TEXTOS_LEGAIS_PUBLICADOS: 'true'`, como o `VITE_CONTATO_NEBRYS`), então os de F14-78 e F14-84 (`legal.test.tsx`) seguem iguais. `legalEscondido.test.tsx` desliga por `vi.mock` de `ambiente` (padrão de `acesso.test.tsx`) e confere: as duas rotas mostram "não encontrada" sem o texto; entrar, aceite de convite do portal, portal com conta e portal por link sem o `nav` "Textos legais". `ambiente.test.ts` ganhou o teste da leitura do valor. `versoes.test.ts` e `prazos.test.ts` não mudam.
- **E2E e bot do manual:** nenhum dos dois cita as páginas ou os links. Sem a variável (como rodam), as capturas das telas de acesso e do portal ficam sem os links no pé; voltam quando a variável existir no build das capturas, se for o caso.
- **Spec:** a coluna Teste de F14-78 e F14-84 não muda (mesmos testes, mesmos nomes).

## Para o F14h (aceite, ainda não feito)

O F14h depende das páginas publicadas: a caixa de aceite do cadastro e do convite da equipe linka os textos e grava a versão aceita, e o "ciente" de versão nova aponta para eles. Com a configuração desligada, um aceite apontaria para página "não encontrada". O F14h precisa tratar isso: ou só entra em produção com `TEXTOS_LEGAIS_PUBLICADOS=true` (depois da #194), ou o aceite também fica atrás da mesma configuração (sem caixa e sem gravar aceite enquanto desligada), com testes nos dois estados. Decisão do Daniel no G2/design do F14h.
