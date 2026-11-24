import { useQuery } from '@tanstack/react-query'
import { useState } from 'react'
import { consultaModoCadastro } from './cadastro'
import { CapaAcesso } from './CapaAcesso'
import { FormularioCadastro } from './FormularioCadastro'
import { FormularioListaEspera, MOTIVO_LISTA_ESPERA } from './FormularioListaEspera'

export const SUBTITULO_CADASTRO = 'Você entra como dona ou dono da empresa e convida a equipe depois.'

/**
 * `/cadastro` sem convite (RF-AUT-08; D11 do F14). O modo vem da API (`GET /auth/cadastro`): aberto, o formulário de
 * cadastro; convite, o da lista de espera no lugar, com o motivo (F14-44). Se o modo não chegar (API fora do ar), o
 * formulário aparece e a API decide no envio: a recusa por falta de convite troca a tela para a lista de espera.
 */
export function TelaCadastro() {
    const modo = useQuery(consultaModoCadastro)
    const [soPorConvite, setSoPorConvite] = useState(false)

    if (modo.isPending)
        return (
            <CapaAcesso titulo="Criar conta">
                <p className="text-sm text-suave">Carregando…</p>
            </CapaAcesso>
        )
    if (modo.data === 'CONVITE' || soPorConvite)
        return (
            // Quem só entra na lista não usa o Acordu: os termos de uso não cabem, a política sim (F14-109).
            <CapaAcesso titulo="Lista de espera" subtitulo={MOTIVO_LISTA_ESPERA} soPrivacidade>
                <FormularioListaEspera />
            </CapaAcesso>
        )
    return (
        <CapaAcesso titulo="Criar conta" subtitulo={SUBTITULO_CADASTRO}>
            <FormularioCadastro aoExigirConvite={() => setSoPorConvite(true)} />
        </CapaAcesso>
    )
}
