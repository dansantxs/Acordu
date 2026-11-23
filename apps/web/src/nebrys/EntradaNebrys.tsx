import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useNavigate } from '@tanstack/react-router'
import { useEffect } from 'react'
import { Form } from 'react-aria-components'
import { ErroApi } from '@/shared/api/problema'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { abrirSessaoOperacao, motivoSemOperacao } from '@/shared/sessao/sessaoOperacao'
import { SeletorTema } from '@/shared/tema/SeletorTema'
import { Alerta } from '@/shared/ui/Alerta'
import { Botao } from '@/shared/ui/Botao'
import { CampoSenha } from '@/shared/ui/CampoSenha'
import { CampoTexto } from '@/shared/ui/CampoTexto'
import { apiNebrys } from './api'

/** A API responde `404` a qualquer recusa da sessão (contrato §17), sem dizer qual: a tela também não diz. */
const RECUSA = 'Não deu para entrar. Confira o e-mail e a senha.'

/**
 * Entrada da área da Nebrys (`/nebrys`, design do F14, "Área no SPA"): com a aparência do `/entrar`, sem link para
 * cadastro, senha esquecida ou textos legais. Abre a sessão de operação (`POST /nebrys/sessao`), que fica só em memória.
 */
export function EntradaNebrys() {
    const navegar = useNavigate()
    const consultas = useQueryClient()
    const motivo = motivoSemOperacao()
    const envio = useMutation({
        mutationFn: async ({ email, senha }: { email: string; senha: string }) => {
            const { data } = await apiNebrys.POST('/api/v1/nebrys/sessao', { body: { email, senha } })
            return data!
        },
        onSuccess: (token) => {
            consultas.removeQueries({ queryKey: ['nebrys'] })
            abrirSessaoOperacao({ accessToken: token.accessToken, expiraEm: token.expiraEm })
            void navegar({ to: '/nebrys/empresas' })
        },
        // A senha não fica no cache das mutações.
        gcTime: 0,
    })
    useEffect(() => {
        document.title = 'Área da Nebrys · Acordu'
    }, [])
    const mensagem = envio.isError
        ? envio.error instanceof ErroApi && envio.error.status === 404
            ? RECUSA
            : mensagemDeErro(envio.error)
        : null

    return (
        <div className="grid min-h-screen place-items-center bg-fundo p-4 sm:p-8">
            <div className="flex w-full max-w-4xl flex-col overflow-hidden rounded-[10px] shadow-xl shadow-black/15 md:min-h-[480px] md:flex-row">
                <aside className="relative flex flex-col justify-between gap-6 bg-lateral p-6 text-lateral-tinta outline-[1.5px] -outline-offset-8 outline-dashed outline-costura md:w-2/5 md:p-10">
                    <div>
                        <span className="font-titulo text-3xl font-semibold italic text-dourado md:text-4xl">
                            acordu
                        </span>
                        <p className="mt-3 max-w-xs text-sm text-lateral-suave">Operação da Nebrys.</p>
                    </div>
                    <p className="hidden text-xs text-lateral-suave md:block">Nebrys</p>
                </aside>
                <main className="flex flex-1 flex-col gap-6 bg-superficie p-6 md:p-10">
                    <div className="flex justify-end">
                        <SeletorTema />
                    </div>
                    <header>
                        <h1 className="font-titulo text-3xl font-semibold tracking-tight">Área da Nebrys</h1>
                        <p className="mt-1 text-sm text-suave">A sessão dura 30 minutos e acaba ao recarregar.</p>
                    </header>
                    <Form
                        className="flex flex-col gap-4"
                        onSubmit={(evento) => {
                            evento.preventDefault()
                            const dados = new FormData(evento.currentTarget)
                            envio.mutate({ email: String(dados.get('email')), senha: String(dados.get('senha')) })
                        }}
                    >
                        {mensagem ? (
                            <Alerta>{mensagem}</Alerta>
                        ) : (
                            motivo === 'expirada' && (
                                <Alerta tom="aviso">A sessão da área acabou. Entre de novo para continuar.</Alerta>
                            )
                        )}
                        <CampoTexto
                            name="email"
                            type="email"
                            rotulo="E-mail"
                            autoComplete="username"
                            isRequired
                            autoFocus
                        />
                        <CampoSenha name="senha" rotulo="Senha" autoComplete="current-password" isRequired />
                        <div className="flex justify-end">
                            <Botao type="submit" isPending={envio.isPending}>
                                Entrar
                            </Botao>
                        </div>
                    </Form>
                </main>
            </div>
        </div>
    )
}
