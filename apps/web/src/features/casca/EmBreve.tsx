import { EstadoVazio } from '@/shared/ui/EstadoVazio'
import { Pagina } from '@/shared/ui/Pagina'

/** Tela que já tem lugar na navegação, mas chega no F2. */
export function EmBreve({ titulo, sobre }: { titulo: string; sobre: string }) {
    return (
        <Pagina titulo={titulo}>
            <EstadoVazio titulo="Esta página ainda está em branco">{sobre}</EstadoVazio>
        </Pagina>
    )
}
