import { Link } from '@tanstack/react-router'
import { useEffect } from 'react'
import { SeletorTema } from '@/shared/tema/SeletorTema'
import { TextoMarcado } from './TextoMarcado'
import { dataDaVigencia, TEXTOS, type TipoTextoLegal } from './textosLegais'

const OUTRO: Record<TipoTextoLegal, { para: '/termos' | '/privacidade'; rotulo: string }> = {
    termos: { para: '/privacidade', rotulo: 'Política de privacidade' },
    privacidade: { para: '/termos', rotulo: 'Termos de uso' },
}

/**
 * Página pública de um texto legal (F14-78): abre sem sessão e sem chamar a API, com a versão e a data de vigência
 * logo abaixo do título.
 */
export function PaginaLegal({ tipo }: { tipo: TipoTextoLegal }) {
    const texto = TEXTOS[tipo]
    const titulo = texto.blocos.find((b) => b.tipo === 'titulo' && b.nivel === 1)
    const nome = titulo?.tipo === 'titulo' ? titulo.texto : ''
    useEffect(() => {
        // Ao sair (para o entrar, ou do termos para a política), a aba volta ao título de antes.
        const anterior = document.title
        document.title = `${nome} · Acordu`
        return () => {
            document.title = anterior
        }
    }, [nome])
    const outro = OUTRO[tipo]
    return (
        <div className="min-h-screen bg-fundo text-tinta">
            <header className="border-b border-linha bg-superficie">
                <div className="mx-auto flex max-w-3xl items-center gap-4 px-4 py-3">
                    <Link to="/" className="mr-auto font-titulo text-2xl font-semibold italic text-acento-texto">
                        acordu
                    </Link>
                    <SeletorTema />
                </div>
            </header>
            <main className="mx-auto flex max-w-3xl flex-col gap-6 px-4 py-8">
                <header className="flex flex-col gap-1">
                    <h1 className="font-titulo text-3xl font-semibold tracking-tight">{nome}</h1>
                    <p className="text-sm text-suave">
                        Versão {texto.versao}, em vigor desde {dataDaVigencia(texto.vigencia)}
                    </p>
                </header>
                <TextoMarcado blocos={texto.blocos} />
                <footer className="flex flex-wrap gap-x-4 gap-y-1 border-t border-linha pt-4 text-sm text-suave">
                    <Link to={outro.para} className="text-acento-texto underline-offset-4 hover:underline">
                        {outro.rotulo}
                    </Link>
                    <Link to="/entrar" className="text-acento-texto underline-offset-4 hover:underline">
                        Entrar no Acordu
                    </Link>
                </footer>
            </main>
        </div>
    )
}
