import { createFileRoute } from '@tanstack/react-router'
import { AvisoConfirmacao } from '@/features/sessao/AvisoConfirmacao'
import { diaPorExtenso, saudacao } from '@/shared/format/saudacao'
import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Pagina } from '@/shared/ui/Pagina'

// Início pessoal (protótipo "Hoje"). O conteúdo — cronômetro, fila do dia, semana, apontado hoje — chega no F2.
export const Route = createFileRoute('/_app/')({ component: Hoje })

function Hoje() {
    const { perfil } = Route.useRouteContext()
    const fuso = perfil.tenant.fusoHorario
    const primeiroNome = perfil.nome.split(' ')[0]
    return (
        <Pagina titulo={`${saudacao(fuso)}, ${primeiroNome}`} contexto={diaPorExtenso(fuso)}>
            <AvisoConfirmacao perfil={perfil} />
            <EstadoVazio titulo="Seu dia aparece aqui">
                O que está na sua mão hoje, o que espera o cliente, sua semana e as horas apontadas. Enquanto isso, use
                Ctrl+K para buscar clientes e solicitações.
            </EstadoVazio>
        </Pagina>
    )
}
