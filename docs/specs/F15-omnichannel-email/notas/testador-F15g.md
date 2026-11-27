# Notas do testador — F15g (etapa 5)

Cenários F15-14, F15-15, F15-98 (design D6, D14, D15). Sem SDK .NET na sessão: os testes C# só rodam na CI; foram lidos contra as regras de análise (nameof, CA1826, CA1859, CA1305, async/await nos helpers).

## Já coberto pelo desenvolvedor (conferido, verifica o "então")
- F15-14: Api (mensagem EXTERNA do contato, sem texto, canal EMAIL, chave no storage, conteúdo no storage), Application, leitor MIME.
- F15-15: Api (tipo, 20 MB, 6º; nota, aviso e registro), Application (`Separar`, textos), integração (registro jsonb, outra empresa nulo), domínio.
- F15-98: Api (portal com conta e pelo link, sem a nota e sem `autorSistema`; avisos sem a nota), domínio (sem evento), front.

## Acrescentado
- `AnexosDaEntradaTests`: aviso com mais de 20 e "e mais N", exatamente 20, singular, nomes de 255 com milhares de recusados cabem em 10.000, executável com nome de imagem, exatamente 5 válidos.
- `ComandosEntradaEmailTests`: repetição com anexos (storage, nota e registro sem duplicar), automático com anexos (nada sobe), falha do storage no meio (o que subiu sai, nada gravado), 30 recusados (nota com teto, registro com todos), nota INTERNA sem evento.
- `AnexosPorEmailTests`: repetição de ponta a ponta, nota fora do sino e do feed, 404 para outra empresa e para outro cliente do portal, log sem nome de arquivo, endereço nem texto da nota.

## Observações (ressalvas)
- Permissão 403: nenhuma rota nova; a leitura da conversa da equipe segue a permissão que já existia (sem teste novo).
- O registro guarda todos os recusados em jsonb (só a nota e o aviso têm teto de 20). Um e-mail de 25 MiB com milhares de partes gera um jsonb grande por 7 dias; vale limitar no F15n (limites) se preocupar.
- A decisão de ordem "tamanho antes do tipo" (executável acima de 20 MB vira "maior que 20 MB") não foi fixada em teste: o design não a define.
- Nada foi executado em .NET; conferência real na CI.
