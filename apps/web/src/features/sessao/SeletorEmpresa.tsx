import { useMutation } from '@tanstack/react-query'
import { Button, Label, ListBox, ListBoxItem, Popover, Select, SelectValue } from 'react-aria-components'
import { mensagemDeErro } from '@/shared/api/mensagens'
import { trocarEmpresa } from '@/shared/sessao/acoes'
import type { Perfil } from '@/shared/sessao/perfil'
import { Alerta } from '@/shared/ui/Alerta'

/** Troca a empresa ativa entre as que a pessoa tem vínculo. Com uma só, mostra o nome e nada mais. */
export function SeletorEmpresa({ perfil }: { perfil: Perfil }) {
    const troca = useMutation({ mutationFn: trocarEmpresa })

    if (perfil.tenants.length < 2) return <span className="text-sm font-semibold">{perfil.tenant.nome}</span>

    return (
        <div className="flex flex-col gap-1">
            <Select
                aria-label="Empresa"
                selectedKey={perfil.tenant.id}
                isDisabled={troca.isPending}
                onSelectionChange={(chave) => {
                    if (chave !== null && chave !== perfil.tenant.id) troca.mutate(String(chave))
                }}
                className="flex flex-col"
            >
                <Label className="sr-only">Empresa</Label>
                <Button className="flex items-center gap-2 rounded-couro border border-linha bg-superficie px-3 py-1.5 text-sm font-semibold hover:bg-trilho">
                    <SelectValue />
                    <span aria-hidden="true">▾</span>
                </Button>
                <Popover className="min-w-[--trigger-width] rounded-couro border border-linha bg-superficie p-1 shadow-lg">
                    <ListBox items={perfil.tenants} className="outline-none">
                        {(tenant) => (
                            <ListBoxItem
                                id={tenant.id}
                                textValue={tenant.nome}
                                className="cursor-default rounded-couro px-3 py-2 text-sm outline-none data-[focused]:bg-trilho data-[selected]:font-semibold"
                            >
                                {tenant.nome}
                                <span className="ml-2 text-xs text-suave">{tenant.papel}</span>
                            </ListBoxItem>
                        )}
                    </ListBox>
                </Popover>
            </Select>
            {troca.isError && <Alerta>{mensagemDeErro(troca.error)}</Alerta>}
        </div>
    )
}
