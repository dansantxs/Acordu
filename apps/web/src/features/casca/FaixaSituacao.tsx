import { FORNECEDOR, textoDoFornecedor } from '@/shared/config/fornecedor'
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
 * TESTE fora da janela: nada (F14-11). Na empresa de demonstração (RF-NEB-02, F14s), que fica fora do ciclo, a faixa
 * diz que os dados são fictícios e que nada sai por e-mail (F14-89).
 */
export function FaixaSituacao({ tenant, agora = new Date() }: { tenant: Tenant; agora?: Date }) {
    if (tenant.demonstracao) return <FaixaDemonstracao />
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

/** A demonstração: dados fictícios, nada sai por e-mail, e o fornecedor pode voltar tudo ao início. */
function FaixaDemonstracao() {
    return (
        <div role="status" className="rounded-couro border border-linha bg-trilho px-4 py-3 text-sm text-suave">
            <strong className="font-semibold text-tinta">Empresa de demonstração.</strong> Os dados são fictícios e nada
            sai daqui por e-mail: convites e avisos não são enviados, e o link de acesso do portal sai só para copiar.{' '}
            {textoDoFornecedor('{{A fornecedor}} pode voltar os dados ao início a qualquer momento.')}
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

/** O canal do fornecedor (decisão 14 do G2, ADR 0017), quando o build o traz (`VITE_CONTATO_FORNECEDOR`). */
function ComoAtivar() {
    const fale = textoDoFornecedor('fale com {{a fornecedor}}')
    if (!FORNECEDOR.contato) return <>{fale}.</>
    return (
        <>
            {fale}:{' '}
            <a href={`mailto:${FORNECEDOR.contato}`} className="font-semibold underline underline-offset-2">
                {FORNECEDOR.contato}
            </a>
            .
        </>
    )
}
