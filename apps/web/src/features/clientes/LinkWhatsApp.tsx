import { Link } from 'react-aria-components'
import { linkWhatsApp } from '@/shared/format/whatsapp'
import { Icone } from '@/shared/ui/Icone'

/**
 * Atalho "Falar no WhatsApp" do cliente ou do contato (#208): abre o `wa.me` numa aba nova, sem integração. Não grava
 * nada, então não leva `grava` (funciona na empresa só leitura). Sem telefone que sirva, não aparece. Fica em
 * `features/clientes`: o portal do cliente não o mostra.
 */
export function LinkWhatsApp({ telefone, nome }: { telefone: string | null | undefined; nome: string }) {
    const href = linkWhatsApp(telefone)
    if (!href) return null
    return (
        <Link
            href={href}
            target="_blank"
            rel="noopener noreferrer"
            aria-label={`Falar no WhatsApp com ${nome} (abre em outra aba)`}
            className="inline-flex items-center gap-1 text-sm text-acento-texto outline-none hover:underline data-[focus-visible]:rounded-sm data-[focus-visible]:ring-2 data-[focus-visible]:ring-acento"
        >
            <Icone nome="conversa" />
            WhatsApp
        </Link>
    )
}
