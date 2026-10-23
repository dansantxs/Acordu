import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { ErroApi } from '@/shared/api/problema'
import { quando } from '@/shared/format/quando'
import { Alerta } from '@/shared/ui/Alerta'
import { consultaSolicitacaoPortal, ESTILO_STATUS_EXTERNO, STATUS_EXTERNO } from './consultas'
import { TituloPortal } from './CascaPortal'

/** Detalhe da solicitação no portal (F7a: o essencial; a conversa e a aprovação chegam no F7b). */
export function DetalhePortal({ id, fuso }: { id: string; fuso: string }) {
    const consulta = useQuery(consultaSolicitacaoPortal(id))
    if (consulta.isPending) return <p className="text-sm text-suave">Carregando…</p>
    if (consulta.isError)
        return (
            <Alerta>
                {consulta.error instanceof ErroApi && consulta.error.status === 404
                    ? 'Não encontramos esta solicitação.'
                    : mensagemDeErro(consulta.error)}
            </Alerta>
        )
    const s = consulta.data
    return (
        <>
            <Link to="/portal" className="text-sm text-acento-texto hover:underline">
                ← Solicitações
            </Link>
            <TituloPortal
                titulo={s.titulo}
                contexto={`#${s.numero} · aberta ${quando(s.abertaEm, fuso)}`}
                acoes={
                    <span className={`rounded-couro px-2 py-1 text-sm ${ESTILO_STATUS_EXTERNO[s.statusExterno]}`}>
                        {STATUS_EXTERNO[s.statusExterno]}
                    </span>
                }
            />
            {s.descricao && <p className="text-sm whitespace-pre-line">{s.descricao}</p>}
        </>
    )
}
