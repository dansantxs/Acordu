import { useQuery } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { useSyncExternalStore } from 'react'
import { CapaAcesso } from '@/features/sessao/CapaAcesso'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { assinarSessaoLink, motivoSemLink, sessaoLink, versaoSessaoLink } from '@/shared/sessao/sessaoLink'
import { Alerta } from '@/shared/ui/Alerta'
import { consultaEu } from './consultas'
import { CascaPortal } from './CascaPortal'
import { DetalhePortal } from './DetalhePortal'

/** Fuso do navegador: a sessão por link não lê o perfil (o `/auth/eu` responde 403 a ela). */
const fusoDoNavegador = () => Intl.DateTimeFormat().resolvedOptions().timeZone || 'America/Sao_Paulo'

const SEM_LINK = {
    nenhum: {
        titulo: 'Abra o link do e-mail de novo',
        texto: 'Por segurança, o acesso sem senha fica só nesta aba e some ao recarregar ou fechar a página. Abra outra vez o link do último e-mail sobre a solicitação.',
    },
    invalido: {
        titulo: 'Este link não vale mais',
        texto: 'Cada e-mail novo traz um link novo e cancela o anterior, e os links vencem com o tempo. Abra o link do e-mail mais recente sobre a solicitação.',
    },
    expirado: {
        titulo: 'O acesso por este link acabou',
        texto: 'O link venceu ou foi trocado por um mais novo enquanto você estava aqui. Abra o link do e-mail mais recente sobre a solicitação.',
    },
    saiu: {
        titulo: 'Você saiu',
        texto: 'O acesso por link desta aba foi encerrado. Para voltar, abra o link do e-mail de novo.',
    },
} as const

/**
 * Acompanhar pela sessão por link (F7d): a solicitação do link, com a conversa, responder e aprovar (as rotas ✓L do
 * contrato §12), na moldura do portal sem navegação. Sem sessão (recarregou, link inválido ou vencido), explica o
 * que fazer.
 */
export function TelaLink() {
    useSyncExternalStore(assinarSessaoLink, versaoSessaoLink)
    const atual = sessaoLink()
    const eu = useQuery({ ...consultaEu, enabled: atual !== null })

    if (!atual) {
        const { titulo, texto } = SEM_LINK[motivoSemLink()]
        return (
            <CapaAcesso titulo={titulo} soPrivacidade>
                <p className="text-sm">{texto}</p>
                <p className="text-sm text-suave">
                    Tem conta no portal?{' '}
                    <Link to="/entrar" className="text-acento-texto underline-offset-4 hover:underline">
                        Entre com e-mail e senha
                    </Link>{' '}
                    para ver todas as suas solicitações.
                </p>
            </CapaAcesso>
        )
    }
    if (eu.isPending) return <p className="p-6 text-sm text-suave">Carregando…</p>
    if (eu.isError) return <Alerta>{mensagemDeErro(eu.error)}</Alerta>
    return (
        <CascaPortal eu={eu.data} link>
            <DetalhePortal id={atual.solicitacaoId} fuso={fusoDoNavegador()} voltar={false} />
            <p className="text-xs text-suave">
                Você está vendo esta solicitação por um link pessoal: não o encaminhe. Ele vale só nesta aba.
            </p>
        </CascaPortal>
    )
}
