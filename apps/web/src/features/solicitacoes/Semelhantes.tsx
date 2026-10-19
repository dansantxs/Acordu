import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { dataHora } from '@/shared/format/prazo'
import { consultaSemelhantes, type FiltrosFila } from './consultas'

/**
 * Resolvidas parecidas (RF-CON-02): para quem atende ver como um caso igual foi resolvido. Só aparece com resultado;
 * abrir uma troca o painel, e voltar no navegador traz a de antes.
 */
export function Semelhantes({ solicitacaoId, fuso }: { solicitacaoId: string; fuso: string }) {
    const semelhantes = useQuery(consultaSemelhantes(solicitacaoId))
    if (!semelhantes.data?.length) return null
    return (
        <section aria-labelledby="semelhantes" className="flex flex-col gap-2">
            <h3 id="semelhantes" className="text-sm font-semibold">
                Parecidas já resolvidas
            </h3>
            <ul className="divide-y divide-linha rounded-couro border border-linha bg-superficie text-sm">
                {semelhantes.data.map((s) => (
                    <li key={s.id} className="flex flex-col gap-0.5 px-3 py-2">
                        <Link
                            to="/solicitacoes/$id"
                            params={{ id: s.id }}
                            search={(atual: FiltrosFila) => atual}
                            className="font-semibold hover:underline"
                        >
                            <span className="text-suave tabular-nums">#{s.numero}</span> {s.titulo}
                        </Link>
                        <p className="line-clamp-2 text-suave">{s.solucaoResumo}</p>
                        <p className="text-xs text-suave">resolvida {dataHora(s.resolvidaEm, fuso)}</p>
                    </li>
                ))}
            </ul>
        </section>
    )
}
