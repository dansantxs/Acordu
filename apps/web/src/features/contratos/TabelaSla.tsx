import { PRIORIDADE, type Prioridade } from '@/features/solicitacoes/rotulos'
import { formatarDuracao, lerDuracao } from '@/shared/format/duracao'
import type { PrazoSla } from './consultas'

export const PRIORIDADES: Prioridade[] = ['URGENTE', 'ALTA', 'MEDIA', 'BAIXA']

/** Textos editáveis ("4h") dos prazos, por `PRIORIDADE.primeira` e `PRIORIDADE.resolucao`. */
export type TextosSla = Record<string, string>

export function textosSla(prazos: readonly PrazoSla[]): TextosSla {
    const valores: TextosSla = {}
    for (const p of PRIORIDADES) {
        const prazo = prazos.find((s) => s.prioridade === p)
        valores[`${p}.primeira`] = prazo ? formatarDuracao(Number(prazo.primeiraRespostaMinutos)) : ''
        valores[`${p}.resolucao`] = prazo ? formatarDuracao(Number(prazo.resolucaoMinutos)) : ''
    }
    return valores
}

/** Lê os textos; `invalidos` traz as chaves que não viraram duração. */
export function lerTextosSla(textos: TextosSla): { prazos: PrazoSla[]; invalidos: string[] } {
    const invalidos: string[] = []
    const prazos = PRIORIDADES.map((p) => {
        const primeira = lerDuracao(textos[`${p}.primeira`] ?? '')
        const resolucao = lerDuracao(textos[`${p}.resolucao`] ?? '')
        if (primeira === null) invalidos.push(`${p}.primeira`)
        if (resolucao === null) invalidos.push(`${p}.resolucao`)
        return { prioridade: p, primeiraRespostaMinutos: primeira ?? 0, resolucaoMinutos: resolucao ?? 0 }
    })
    return { prazos, invalidos }
}

/** Tabela dos prazos das quatro prioridades, no campo de duração (contrato e SLA padrão da empresa). */
export function TabelaSla({
    valores,
    aoMudar,
    invalidos,
}: {
    valores: TextosSla
    aoMudar: (valores: TextosSla) => void
    invalidos: string[]
}) {
    return (
        <>
            <table className="w-full text-sm">
                <thead className="text-left text-xs text-suave">
                    <tr>
                        <th className="py-1 font-semibold">Prioridade</th>
                        <th className="py-1 font-semibold">1ª resposta</th>
                        <th className="py-1 font-semibold">Solução</th>
                    </tr>
                </thead>
                <tbody>
                    {PRIORIDADES.map((p) => (
                        <tr key={p}>
                            <th scope="row" className="py-1 pr-3 text-left font-normal">
                                {PRIORIDADE[p]}
                            </th>
                            {(['primeira', 'resolucao'] as const).map((campo) => {
                                const chave = `${p}.${campo}`
                                return (
                                    <td key={campo} className="py-1 pr-3">
                                        <input
                                            aria-label={`${campo === 'primeira' ? '1ª resposta' : 'Solução'} ${PRIORIDADE[p]}`}
                                            value={valores[chave] ?? ''}
                                            onChange={(e) => aoMudar({ ...valores, [chave]: e.target.value })}
                                            aria-invalid={invalidos.includes(chave) || undefined}
                                            className="w-24 rounded-couro border border-linha bg-superficie px-2 py-1.5 text-sm text-tinta aria-[invalid]:border-vermelho"
                                        />
                                    </td>
                                )
                            })}
                        </tr>
                    ))}
                </tbody>
            </table>
            {invalidos.some((i) => i.includes('.')) && (
                <p className="text-sm text-vermelho">Confira os prazos marcados: use 4h, 1h30 ou 90.</p>
            )}
        </>
    )
}
