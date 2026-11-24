# Notas do testador (F14r, etapa 5)

Sem SDK .NET nesta sessão: o C# novo foi escrito para compilar, e só a CI o roda. O front foi rodado.

## Cobertura conferida
F14-91 a F14-96 já tinham teste do "então" em quatro camadas (funcional, integração, unidade, front). Conferidos: 403 (membro e contato, GET/POST/DELETE), isolamento A/B (lista, detalhe 404, apagar em B não toca A), cliente real criado depois sobrevive, 422 `transicao-invalida` (dado próprio, repetido, ativa), 422 `empresa-somente-leitura` (VENCIDA e CANCELADA, carregar e apagar), nenhum e-mail na carga (outbox e caixa), interação externa e alerta de SLA em exemplo, JSON `.invalid` sem CPF/CNPJ, datas relativas, bot do manual.

## Lacunas fechadas
- `ExemplosTests › FechamentoAutomatico_ResolvidasDoExemplo_FechamSemEmail`: o job fecha as resolvidas dos exemplos e nenhum e-mail sai ao contato fictício.
- `DadosDeExemploTests › RemoverAsync_...`: agora com comentário e seguidor de tarefa de exemplo (saem) e de tarefa de cliente real (ficam); a tarefa real também fica.
- `clientes/exemplo.test.tsx`: etiqueta "exemplo" na lista (só no cliente de exemplo), aviso na ficha, sem aviso em cliente real.

## Observações (para a descrição do PR)
- Não há teste de que o apagar não manda e-mail e de que a remoção não toca sino/notificações (ficam, por decisão do dev). Sem tela que reverta.
- A oferta some ao convidar alguém para a equipe (decisão em aberto no `estado.md`).
- Concorrência de dois POST simultâneos é risco aceito, sem teste.
