import { caminhoDaCaptura, TAMANHO_CAPTURA } from '@/features/ajuda/capturas'
import { Captura } from '@/shared/ui/Captura'
import { SECOES_AJUDA_PORTAL } from './ajudaPortal'
import { TituloPortal } from './CascaPortal'
import type { PortalEu } from './consultas'

/**
 * Ajuda do portal (M3): o guia curto do cliente, na mesma moldura. Contratos só aparece para quem vê contratos, como
 * a própria tela.
 */
export function AjudaPortal({ eu }: { eu: PortalEu }) {
    const secoes = SECOES_AJUDA_PORTAL.filter((s) => !s.soComContratos || eu.veContratos)
    return (
        <>
            <TituloPortal titulo="Ajuda" contexto={`Como usar o portal de ${eu.prestador.nome}`} />
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
