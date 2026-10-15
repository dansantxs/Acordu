import { createFileRoute, redirect } from '@tanstack/react-router'
import { CapaAcesso } from '@/features/sessao/CapaAcesso'
import { sair } from '@/shared/sessao/acoes'
import { aguardarSessao } from '@/shared/sessao/sessao'
import { Botao } from '@/shared/ui/Botao'

// Provisório: o portal do cliente tem marco próprio. Por ora o contato que entra pela tela de login cai aqui.
export const Route = createFileRoute('/portal/')({
    beforeLoad: async ({ location }) => {
        const estado = await aguardarSessao()
        if (estado.situacao !== 'ativa') throw redirect({ to: '/entrar', search: { voltar: location.href } })
    },
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
