import type { ReactNode } from 'react'

type Tom = 'erro' | 'aviso' | 'sucesso'

const estilos: Record<Tom, string> = {
    erro: 'border-vermelho/40 bg-vermelho-suave text-vermelho',
    aviso: 'border-ambar/40 bg-ambar-suave text-ambar-texto',
    sucesso: 'border-verde/40 bg-verde-suave text-verde',
}

/** Mensagem em bloco; erro é anunciado na hora (`role="alert"`), os demais com educação (`status`). */
export function Alerta({ tom = 'erro', children }: { tom?: Tom; children: ReactNode }) {
    return (
        <div
            role={tom === 'erro' ? 'alert' : 'status'}
            className={`rounded-couro border px-3 py-2 text-sm ${estilos[tom]}`}
        >
            {children}
        </div>
    )
}
