import { CONTATO_NEBRYS } from '@/shared/config/ambiente'
import {
    dataNoFuso,
    diasRestantesDoTeste,
    ID_MOTIVO_SEM_GRAVACAO,
    podeGravar,
    ultimoDiaDoTeste,
    type Tenant,
} from '@/shared/sessao/gravacao'

/**
 * Faixa da situação da empresa no topo de toda tela interna (spec do F14): nos últimos dias do teste, quantos faltam e
 * como ativar (F14-10); só leitura, desde quando, até quando os dados ficam e como ativar (F14-25). Empresa ATIVA ou em
 * TESTE fora da janela: nada (F14-11).
 */
export function FaixaSituacao({ tenant, agora = new Date() }: { tenant: Tenant; agora?: Date }) {
    if (!podeGravar(tenant)) return <FaixaSomenteLeitura tenant={tenant} />
    const dias = diasRestantesDoTeste(tenant, agora)
    if (dias === null || !tenant.fimDoTeste) return null
    const ate = ultimoDiaDoTeste(tenant.fimDoTeste, tenant.fusoHorario)
    const quanto =
        dias === 0
            ? `O teste do Acordu termina hoje (${ate}).`
            : `${dias === 1 ? 'Falta 1 dia' : `Faltam ${dias} dias`} para o fim do teste do Acordu (vai até ${ate}).`
    return (
        <div
            role="status"
            className="rounded-couro border border-ambar/40 bg-ambar-suave px-4 py-3 text-sm text-ambar-texto"
        >
            <strong className="font-semibold">{quanto}</strong> Depois disso a empresa fica só leitura. Para continuar
            usando, <ComoAtivar />
        </div>
    )
}

function FaixaSomenteLeitura({ tenant }: { tenant: Tenant }) {
    const fuso = tenant.fusoHorario
    const desde = tenant.somenteLeituraDesde ? ` desde ${dataNoFuso(tenant.somenteLeituraDesde, fuso)}` : ''
    const porque = tenant.situacao === 'CANCELADA' ? 'A conta foi cancelada' : 'O teste terminou'
    return (
        <div
            role="status"
            className="rounded-couro border border-vermelho/40 bg-vermelho-suave px-4 py-3 text-sm text-vermelho"
        >
            <strong id={ID_MOTIVO_SEM_GRAVACAO} className="font-semibold">
                {`${porque}, e a empresa está só leitura${desde}: dá para consultar tudo, mas não gravar.`}
            </strong>{' '}
            {tenant.expurgoPrevistoEm &&
                `Os dados ficam guardados até ${dataNoFuso(tenant.expurgoPrevistoEm, fuso)}; depois disso são apagados. `}
            Para ativar, <ComoAtivar />
        </div>
    )
}

/** O canal da Nebrys (decisão 14 do G2), quando o build o traz (`VITE_CONTATO_NEBRYS`). */
function ComoAtivar() {
    if (!CONTATO_NEBRYS) return <>fale com a Nebrys.</>
    return (
        <>
            fale com a Nebrys:{' '}
            <a href={`mailto:${CONTATO_NEBRYS}`} className="font-semibold underline underline-offset-2">
                {CONTATO_NEBRYS}
            </a>
            .
        </>
    )
}
