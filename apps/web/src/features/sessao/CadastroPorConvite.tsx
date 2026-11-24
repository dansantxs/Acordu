import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { CONTATO_NEBRYS } from '@/shared/config/ambiente'
import { Alerta } from '@/shared/ui/Alerta'
import { consultaConviteEmpresa } from './cadastro'
import { FormularioCadastro } from './FormularioCadastro'
import { linkRecusado } from './linkInvalido'

const FALE_COM_A_NEBRYS = CONTATO_NEBRYS ? `fale com a Nebrys: ${CONTATO_NEBRYS}` : 'fale com a Nebrys'

const estiloLink = 'text-sm text-acento-texto underline-offset-4 hover:underline'

/**
 * Cadastro aberto pelo link do convite de empresa (`/cadastro/convite/{token}`; RF-AUT-08, F14-46). A consulta traz o
 * e-mail do convite, que o formulário mostra sem deixar trocar (pergunta 7 do G2), e se ele já tem conta: nesse caso o
 * cadastro seria recusado (F14-105, decisão 18 do G2), e a tela explica em vez de mostrar o formulário. Convite que não
 * vale (`401`, sem distinguir o motivo) diz como pedir outro.
 */
export function CadastroPorConvite({ token }: { token: string | null }) {
    const convite = useQuery({ ...consultaConviteEmpresa(token ?? ''), enabled: token !== null })

    if (token === null || linkRecusado(convite.error))
        return (
            <div className="flex flex-col gap-4">
                <Alerta>
                    Este convite não vale mais: ele vence em 7 dias, só pode ser usado uma vez e pode ter sido revogado.
                    Para pedir outro, {FALE_COM_A_NEBRYS}.
                </Alerta>
                <Link to="/cadastro" className={estiloLink}>
                    Ir para o cadastro
                </Link>
            </div>
        )
    if (convite.isPending) return <p className="text-sm text-suave">Carregando o convite…</p>
    if (convite.isError) return <Alerta>{mensagemDeErro(convite.error)}</Alerta>

    const { email, temConta } = convite.data
    if (temConta)
        return (
            <div className="flex flex-col gap-4">
                <Alerta tom="aviso">
                    O e-mail <strong>{email}</strong> já tem conta no Acordu, e a empresa nova precisa ser criada com um
                    e-mail que ainda não tem. Para receber o convite em outro e-mail, {FALE_COM_A_NEBRYS}.
                </Alerta>
                <Link to="/entrar" className={estiloLink}>
                    Entrar com a conta que já existe
                </Link>
            </div>
        )

    return (
        <div className="flex flex-col gap-4">
            <p className="text-sm">
                A Nebrys convidou <strong>{email}</strong> para criar uma empresa no Acordu.
            </p>
            <FormularioCadastro convite={{ token, email }} />
        </div>
    )
}
