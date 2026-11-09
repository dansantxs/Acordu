import {
    Button,
    UNSTABLE_Toast as Toast,
    UNSTABLE_ToastContent as ConteudoAviso,
    UNSTABLE_ToastRegion as Regiao,
} from 'react-aria-components'
import { Icone } from '../ui/Icone'
import { filaDeAvisos } from './avisos'

/** Onde os avisos aparecem: embaixo, no centro (protótipo). F6 leva o foco até eles. */
export function RegiaoAvisos() {
    return (
        <Regiao
            queue={filaDeAvisos}
            aria-label="Avisos"
            className="fixed inset-x-0 bottom-4 z-[60] flex flex-col-reverse items-center gap-2 px-4 outline-none"
        >
            {({ toast }) => (
                <Toast
                    toast={toast}
                    className={`flex w-full max-w-md items-center gap-3 rounded-couro border px-4 py-3 text-sm shadow-xl outline-none ${
                        toast.content.tom === 'erro'
                            ? 'border-vermelho/40 bg-vermelho-suave text-vermelho'
                            : 'border-linha bg-superficie text-tinta'
                    }`}
                >
                    <ConteudoAviso className="mr-auto">{toast.content.texto}</ConteudoAviso>
                    {toast.content.desfazer && (
                        <Button
                            onPress={() => {
                                filaDeAvisos.close(toast.key)
                                void toast.content.desfazer?.()
                            }}
                            className="font-semibold text-acento-texto underline underline-offset-4"
                        >
                            Desfazer
                        </Button>
                    )}
                    {toast.content.acao && (
                        <Button
                            onPress={() => {
                                filaDeAvisos.close(toast.key)
                                void toast.content.acao?.executar()
                            }}
                            className="font-semibold text-acento-texto underline underline-offset-4"
                        >
                            {toast.content.acao.rotulo}
                        </Button>
                    )}
                    <Button
                        slot="close"
                        aria-label="Fechar"
                        className="grid size-6 place-items-center rounded-couro hover:bg-trilho"
                    >
                        <Icone nome="fechar" className="size-3.5" />
                    </Button>
                </Toast>
            )}
        </Regiao>
    )
}
