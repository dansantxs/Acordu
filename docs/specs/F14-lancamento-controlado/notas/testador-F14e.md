# Notas do testador (F14e, etapa 5, 1ª passada)

Cenários: F14-10, F14-11, F14-12, F14-25 a F14-28. Todos cobertos; coluna Teste da spec preenchida.

Acrescentado pelo testador:
- `apps/web/src/features/casca/faixaSituacao.test.tsx`: limites da janela de 7 dias com o relógio injetado (8, 7, 1 e 0 dias; prorrogação para dentro e para fora da janela; ATIVA com fim próximo).
- `situacao.test.tsx` de Configurações: quem não gerencia a configuração não vê a situação (Sem acesso a esta seção).

Observações (não bloqueiam):
- Os botões "indisponíveis" cobrem as telas representativas (clientes, horas, cronômetro, convidar, portal). Controles que não são `Botao` (menus "⋯", campos que salvam ao mudar, links de texto) dependem da recusa da API (F14-27), como as notas do desenvolvedor já dizem; não há teste desses.
- A faixa mede a janela por instante e os dias por dia civil no fuso: "Faltam 7 dias" pode aparecer com 7 dias exatos; coberto.
- Pendência de ambiente: `CONTATO_NEBRYS` no GitHub antes do deploy (nota do desenvolvedor).
