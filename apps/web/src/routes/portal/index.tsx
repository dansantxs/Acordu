import { createFileRoute, redirect } from '@tanstack/react-router'
import { CapaAcesso } from '@/features/sessao/CapaAcesso'
import { sair } from '@/shared/sessao/acoes'
import { aguardarSessao } from '@/shared/sessao/sessao'
import { SessaoIndisponivel } from '@/shared/sessao/SessaoIndisponivel'
import { Botao } from '@/shared/ui/Botao'
import { TelaSemConexao } from '@/shared/ui/TelaSemConexao'

// Provisório: o portal do cliente tem marco próprio. Por ora o contato que entra pela tela de login cai aqui.
export const Route = createFileRoute('/portal/')({
    beforeLoad: async ({ location }) => {
        const estado = await aguardarSessao()
        if (estado.situacao === 'indisponivel') throw new SessaoIndisponivel()
        if (estado.situacao !== 'ativa') throw redirect({ to: '/entrar', search: { voltar: location.href } })
    },
    errorComponent: TelaSemConexao,
    component: () => (
        <CapaAcesso titulo="Portal do cliente" subtitulo="O portal ainda está em construção.">
            <div>
                <Botao variante="secundario" onPress={() => void sair()}>
                    Sair
                </Botao>
            </div>
        </CapaAcesso>
    ),
})
