/** Etiquetas do cliente como selos (ficha e lista). */
export function Etiquetas({ etiquetas }: { etiquetas: string[] }) {
    return (
        <ul className="flex flex-wrap gap-1.5" aria-label="Etiquetas">
            {etiquetas.map((e) => (
                <li key={e} className="rounded-couro bg-acento-suave px-2 py-0.5 text-xs text-acento-texto">
                    {e}
                </li>
            ))}
        </ul>
    )
}
