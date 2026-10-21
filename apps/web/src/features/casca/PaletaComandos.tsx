import { useQuery } from '@tanstack/react-query'
import { useRouter } from '@tanstack/react-router'
import { useEffect, useState } from 'react'
import {
    Autocomplete,
    Dialog,
    Header,
    Input,
    Menu,
    MenuItem,
    MenuSection,
    Modal,
    ModalOverlay,
    SearchField,
} from 'react-aria-components'
import { api } from '@/shared/api/cliente'
import { contem } from '@/shared/format/texto'
import { useAtrasado } from '@/shared/hooks/useAtrasado'
import type { Perfil } from '@/shared/sessao/perfil'
import { Icone } from '@/shared/ui/Icone'
import { navegacaoDe, pode } from './navegacao'

/** Ações de criar: cada uma abre a tela com a janela aberta (`?nova=1`, `?novo=1`). */
const CRIACOES = [
    { rotulo: 'Nova solicitação', para: '/solicitacoes?nova=1', permissao: 'solicitacao.criar' },
    { rotulo: 'Nova tarefa', para: '/tarefas?nova=1', permissao: 'tarefa.criar' },
    { rotulo: 'Novo cliente', para: '/clientes?novo=1', permissao: 'cliente.gerenciar' },
] as const

/** A busca na API começa com 2 letras; antes disso só os destinos do menu. */
const MINIMO_BUSCA = 2
const LIMITE = 5

const estiloItem =
    'flex cursor-default items-center justify-between gap-3 rounded-couro px-3 py-2 text-sm outline-none data-[focused]:bg-trilho'
const estiloSecao = 'px-3 pt-3 pb-1 text-xs font-semibold tracking-wide text-suave uppercase'

/** Abre com Ctrl+K (⌘K no Mac) de qualquer tela; Esc fecha. */
export function useAtalhoPaleta(): [boolean, (aberta: boolean) => void] {
    const [aberta, setAberta] = useState(false)
    useEffect(() => {
        function aoTeclar(evento: KeyboardEvent) {
            if ((evento.ctrlKey || evento.metaKey) && evento.key.toLowerCase() === 'k') {
                evento.preventDefault()
                setAberta((atual) => !atual)
            }
        }
        window.addEventListener('keydown', aoTeclar)
        return () => window.removeEventListener('keydown', aoTeclar)
    }, [])
    return [aberta, setAberta]
}

/**
 * Busca e navegação rápida: destinos do menu, clientes e solicitações (pela `busca` das listas da API). Cada grupo
 * só aparece com a permissão de leitura correspondente; as ações de criar, com a permissão de criar.
 */
export function PaletaComandos({
    perfil,
    aberta,
    aoMudar,
}: {
    perfil: Perfil
    aberta: boolean
    aoMudar: (aberta: boolean) => void
}) {
    const roteador = useRouter()
    const [termo, setTermo] = useState('')
    const busca = useAtrasado(termo.trim())
    const buscar = aberta && busca.length >= MINIMO_BUSCA

    const clientes = useQuery({
        queryKey: ['paleta', 'clientes', busca],
        queryFn: async () =>
            (await api.GET('/api/v1/clientes', { params: { query: { busca, tamanhoPagina: LIMITE } } })).data!.itens,
        enabled: buscar && pode(perfil, 'cliente.ver'),
        staleTime: 30_000,
    })
    const solicitacoes = useQuery({
        queryKey: ['paleta', 'solicitacoes', busca],
        queryFn: async () =>
            (await api.GET('/api/v1/solicitacoes', { params: { query: { busca, tamanhoPagina: LIMITE } } })).data!
                .itens,
        enabled: buscar && pode(perfil, 'solicitacao.ver'),
        staleTime: 30_000,
    })

    const destinos = navegacaoDe(perfil).filter((item) => !termo.trim() || contem(item.rotulo, termo))
    const criacoes = CRIACOES.filter((c) => pode(perfil, c.permissao) && (!termo.trim() || contem(c.rotulo, termo)))
    const listaClientes = buscar ? (clientes.data ?? []) : []
    const listaSolicitacoes = buscar ? (solicitacoes.data ?? []) : []
    const carregando = buscar && (clientes.isFetching || solicitacoes.isFetching)
    const vazio = criacoes.length + destinos.length + listaClientes.length + listaSolicitacoes.length === 0

    function ir(caminho: string) {
        aoMudar(false)
        setTermo('')
        roteador.history.push(caminho)
    }

    return (
        <ModalOverlay
            isOpen={aberta}
            onOpenChange={(estado) => {
                aoMudar(estado)
                if (!estado) setTermo('')
            }}
            isDismissable
            className="fixed inset-0 z-50 flex items-start justify-center bg-[var(--veu)] px-4 pt-[12vh]"
        >
            <Modal className="w-full max-w-xl rounded-couro border border-linha bg-superficie text-tinta shadow-2xl">
                <Dialog aria-label="Buscar ou ir para" className="outline-none">
                    <Autocomplete inputValue={termo} onInputChange={setTermo}>
                        <SearchField
                            aria-label="Buscar"
                            autoFocus
                            className="flex items-center gap-2 border-b border-linha px-4"
                        >
                            <Icone nome="busca" className="size-4 text-suave" />
                            <Input
                                placeholder="Buscar cliente, solicitação ou tela…"
                                className="w-full bg-transparent py-3.5 text-base outline-none placeholder:text-suave [&::-webkit-search-cancel-button]:hidden"
                            />
                            {carregando && <span className="text-xs text-suave">buscando…</span>}
                        </SearchField>
                        <Menu
                            aria-label="Resultados"
                            className="max-h-[50vh] overflow-y-auto p-1 outline-none"
                            onAction={(chave) => ir(String(chave))}
                            renderEmptyState={() => (
                                <p className="px-3 py-6 text-sm text-suave">
                                    {vazio && termo.trim().length < MINIMO_BUSCA
                                        ? 'Digite para buscar.'
                                        : carregando
                                          ? 'Buscando…'
                                          : `Nada encontrado para “${termo.trim()}”.`}
                                </p>
                            )}
                        >
                            {criacoes.length > 0 && (
                                <MenuSection>
                                    <Header className={estiloSecao}>Criar</Header>
                                    {criacoes.map((c) => (
                                        <MenuItem key={c.para} id={c.para} textValue={c.rotulo} className={estiloItem}>
                                            <span className="flex items-center gap-2">
                                                <Icone nome="mais" className="size-4 text-suave" />
                                                {c.rotulo}
                                            </span>
                                        </MenuItem>
                                    ))}
                                </MenuSection>
                            )}
                            {destinos.length > 0 && (
                                <MenuSection>
                                    <Header className={estiloSecao}>Ir para</Header>
                                    {destinos.map((item) => (
                                        <MenuItem
                                            key={item.para}
                                            id={item.para}
                                            textValue={item.rotulo}
                                            className={estiloItem}
                                        >
                                            {item.rotulo}
                                            <Icone nome="seta" className="size-4 text-suave" />
                                        </MenuItem>
                                    ))}
                                </MenuSection>
                            )}
                            {listaSolicitacoes.length > 0 && (
                                <MenuSection>
                                    <Header className={estiloSecao}>Solicitações</Header>
                                    {listaSolicitacoes.map((s) => (
                                        <MenuItem
                                            key={s.id}
                                            id={`/solicitacoes/${s.id}`}
                                            textValue={`#${s.numero} ${s.titulo}`}
                                            className={estiloItem}
                                        >
                                            <span className="truncate">
                                                <span className="text-suave">#{s.numero}</span> {s.titulo}
                                            </span>
                                            <span className="shrink-0 text-xs text-suave">{s.cliente.nome}</span>
                                        </MenuItem>
                                    ))}
                                </MenuSection>
                            )}
                            {listaClientes.length > 0 && (
                                <MenuSection>
                                    <Header className={estiloSecao}>Clientes</Header>
                                    {listaClientes.map((c) => (
                                        <MenuItem
                                            key={c.id}
                                            id={`/clientes/${c.id}`}
                                            textValue={c.nome}
                                            className={estiloItem}
                                        >
                                            {c.nome}
                                            {!c.ativo && <span className="text-xs text-suave">inativo</span>}
                                        </MenuItem>
                                    ))}
                                </MenuSection>
                            )}
                        </Menu>
                    </Autocomplete>
                </Dialog>
            </Modal>
        </ModalOverlay>
    )
}
