import { caminhoDaCaptura, TAMANHO_CAPTURA } from '@/features/ajuda/capturas'
import { ListaNotas } from '@/features/novidades/ListaNotas'
import { notasDo } from '@/features/novidades/notas'
import { marcarVistas } from '@/features/novidades/vistas'
import { Captura } from '@/shared/ui/Captura'
import { useEffect } from 'react'
import { SECOES_AJUDA_PORTAL } from './ajudaPortal'
import { TituloPortal } from './CascaPortal'
import type { PortalEu } from './consultas'

/**
 * Ajuda do portal (M3): o guia curto do cliente, na mesma moldura. Contratos só aparece para quem vê contratos, como
 * a própria tela. No alto, as novidades para o cliente (M4); abrir a Ajuda apaga o ponto do menu.
 */
export function AjudaPortal({ eu }: { eu: PortalEu }) {
    const secoes = SECOES_AJUDA_PORTAL.filter((s) => !s.soComContratos || eu.veContratos)
    const notas = notasDo('cliente').slice(0, 3)
    useEffect(() => marcarVistas('cliente'), [])
    return (
        <>
            <TituloPortal titulo="Ajuda" contexto={`Como usar o portal de ${eu.prestador.nome}`} />
            {notas.length > 0 && (
                <section aria-labelledby="ajuda-portal-novidades" className="flex flex-col gap-3">
                    <h2 id="ajuda-portal-novidades" className="font-titulo text-2xl font-semibold tracking-tight">
                        Novidades
                    </h2>
                    <ListaNotas notas={notas} nivel={3} />
                </section>
            )}
            {secoes.map((s) => (
                <section
                    key={s.id}
                    aria-labelledby={`ajuda-portal-${s.id}`}
                    className="flex flex-col gap-3 rounded-couro border border-linha bg-superficie p-4"
                >
                    <h2 id={`ajuda-portal-${s.id}`} className="text-lg font-semibold">
                        {s.titulo}
                    </h2>
                    <p className="text-sm">{s.resumo}</p>
                    {s.passos && (
                        <ol className="flex list-decimal flex-col gap-1 pl-5 text-sm">
                            {s.passos.map((p) => (
                                <li key={p}>{p}</li>
                            ))}
                        </ol>
                    )}
                    {s.imagens?.map((img) => (
                        <Captura
                            key={img.captura}
                            caminho={caminhoDaCaptura(img.captura)}
                            legenda={img.legenda}
                            largura={TAMANHO_CAPTURA.largura}
                            altura={TAMANHO_CAPTURA.altura}
                        />
                    ))}
                    {s.dicas && (
                        <ul className="flex list-disc flex-col gap-1 pl-5 text-sm text-suave">
                            {s.dicas.map((d) => (
                                <li key={d}>{d}</li>
                            ))}
                        </ul>
                    )}
                </section>
            ))}
            <p className="text-sm text-suave">Não achou o que procurava? Abra uma solicitação e pergunte à equipe.</p>
        </>
    )
}
