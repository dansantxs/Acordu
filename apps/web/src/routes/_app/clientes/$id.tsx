import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { ABAS_CLIENTE, type AbaCliente } from '@/features/clientes/abas'
import { FichaCliente } from '@/features/clientes/FichaCliente'

/** Ficha do cliente; a aba fica no endereço (`?secao=contratos`; `aba` já é o filtro de tarefas e o nome repetido quebra o tipo de `to: '.'`) para voltar ao mesmo ponto (decisão 1 do F4). */
export const Route = createFileRoute('/_app/clientes/$id')({
    validateSearch: (busca: Record<string, unknown>): { secao?: AbaCliente; pagina?: number } => ({
        secao:
            ABAS_CLIENTE.includes(busca.secao as AbaCliente) && busca.secao !== 'resumo'
                ? (busca.secao as AbaCliente)
                : undefined,
        // Só a aba Arquivos pagina; trocar de aba volta para a primeira.
        pagina: Number(busca.pagina) > 1 ? Math.floor(Number(busca.pagina)) : undefined,
    }),
    component: Cliente,
})

function Cliente() {
    const { perfil } = Route.useRouteContext()
    const { id } = Route.useParams()
    const { secao, pagina } = Route.useSearch()
    const navegar = useNavigate()
    // `key`: trocar de cliente pela paleta não reaproveita o estado das janelas do anterior.
    return (
        <FichaCliente
            key={id}
            perfil={perfil}
            id={id}
            aba={secao}
            pagina={pagina}
            aoMudarAba={(nova) =>
                void navegar({
                    to: '/clientes/$id',
                    params: { id },
                    search: { secao: nova === 'resumo' ? undefined : nova },
                    replace: true,
                })
            }
        />
    )
}
