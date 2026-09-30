# ADR 0003 — SLA em horas úteis, com calendário por tenant e pausa em "aguardando cliente"

- **Status:** Aceito
- **Data:** 30/09/2026
- **Contexto do projeto:** Acordu (v1)

## Contexto

O SLA (prazo de primeira resposta e de resolução) é exibido ao cliente no portal e usado internamente para priorizar a fila. Se o número estiver errado, o produto perde exatamente o que vende: confiança e transparência.

Prestadores pequenos trabalham em horário comercial. Um prazo de "8 horas" aberto sexta às 17h não vence sexta à 1h da manhã; vence segunda no meio da manhã. Também não é justo o relógio correr enquanto o prestador espera uma informação do próprio cliente.

## Decisão

1. **Unidade:** prazos definidos em **minutos úteis**, por prioridade, na `PoliticaSla` do contrato. Sem contrato, a solicitação não tem SLA.

2. **Calendário por tenant (`CalendarioUtil`):** fuso horário IANA, uma janela de expediente por dia da semana e lista de feriados. Semeado com seg–sex 08:00–18:00, `America/Sao_Paulo`, sem feriados. Feriados são cadastrados pelo tenant (sem importar calendários externos no v1).

3. **Cálculo** num serviço de domínio puro (`CalculadoraSla`): anda em hora local do tenant, pula fora do expediente, fins de semana e feriados, e converte o resultado para UTC. Instantes persistidos sempre em UTC.

4. **Pausa:** entrar em AGUARDANDO_CLIENTE congela o prazo de resolução (guarda os minutos úteis restantes); ao retomar, o prazo é recalculado a partir do instante da retomada. AGUARDANDO_TERCEIRO **não** pausa (é responsabilidade do prestador). O prazo de primeira resposta nunca pausa.

5. **Situação:** NO_PRAZO, EM_RISCO (≥ 75% do tempo útil consumido) e VIOLADO, calculada sob demanda a partir do relógio persistido. Alertas de risco/violação saem de um job (HostedService) que roda a cada poucos minutos e é idempotente (marca o alerta como enviado).

6. **Reabertura:** novo ciclo de resolução com prazo cheio a partir da reabertura.

## Consequências

**Positivas**
- Prazos que batem com a percepção do cliente e do prestador.
- Determinístico e testável: mesma entrada (calendário, instante, minutos) → mesmo prazo. Nenhum "agora" escondido no domínio.
- Explicável no portal: "prazo: seg 10:00 (8h úteis a partir de sex 17:00)".

**Negativas / riscos**
- É a parte com mais casos de borda do v1: virada de dia/semana, feriado emendado, início fora do expediente, horário de verão (se voltar a existir), mudança de calendário com solicitações abertas. Mitigação: bateria de testes unitários dedicada; prazos **não** são recalculados retroativamente quando o calendário muda (valem para novas solicitações e retomadas).
- Expediente com intervalo de almoço e janelas diferentes por equipe ficam fora do v1.

## Alternativas consideradas

- **Horas corridas (24×7):** trivial, mas gera prazos absurdos para prestadores de horário comercial. Rejeitado; pode virar opção por contrato no futuro ("SLA 24×7").
- **Pausar também em AGUARDANDO_TERCEIRO:** deixaria o prestador esconder atraso atrás de fornecedor. Rejeitado.
- **Biblioteca externa de dias úteis/feriados:** não resolve expediente por dia nem feriados municipais; o cálculo é pequeno o bastante para ser próprio e testado.
