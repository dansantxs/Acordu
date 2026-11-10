// Service worker do Acordu (spec do F12, F12f, decisão D7). Mínimo de propósito:
// - guarda só a página "Sem conexão" e o que ela usa; nunca guarda resposta da API nem páginas do app (isolamento
//   entre empresas e clientes num aparelho compartilhado; segurança §6);
// - navegação vai sempre à rede (versão nova vale no próximo carregamento, F12-29); sem rede, mostra "Sem conexão"
//   (F12-28);
// - mostra a notificação do sistema onde a página não pode (Android, F12c) e abre o item no clique.
// Servido sem cache (_headers). Mudou a casca offline? Troque a VERSAO.
const VERSAO = 'acordu-casca-1'
const CASCA = ['/offline.html', '/offline.css', '/icones/icone-192.png']

self.addEventListener('install', (evento) => {
    evento.waitUntil(
        caches
            .open(VERSAO)
            .then((cache) => cache.addAll(CASCA))
            .then(() => self.skipWaiting()),
    )
})

self.addEventListener('activate', (evento) => {
    evento.waitUntil(
        caches
            .keys()
            .then((chaves) => Promise.all(chaves.filter((c) => c !== VERSAO).map((c) => caches.delete(c))))
            .then(() => self.clients.claim()),
    )
})

self.addEventListener('fetch', (evento) => {
    const pedido = evento.request
    // Navegação: sempre a rede; sem rede, "Sem conexão".
    if (pedido.mode === 'navigate') {
        evento.respondWith(fetch(pedido).catch(() => caches.match('/offline.html', { cacheName: VERSAO })))
        return
    }
    // O que a página "Sem conexão" usa (caminho exato, mesma origem): rede primeiro, cache sem rede. O resto (API,
    // arquivos do app) segue direto, sem cache do service worker.
    const url = new URL(pedido.url)
    if (url.origin !== self.location.origin || !CASCA.includes(url.pathname)) return
    evento.respondWith(fetch(pedido).catch(() => caches.match(url.pathname, { cacheName: VERSAO })))
})

/** Só caminho interno do app ("/x"), nunca outra origem ("//x", "https://x"). */
function caminhoInterno(valor) {
    return typeof valor === 'string' && valor.startsWith('/') && !valor.startsWith('//') && !valor.startsWith('/\\')
        ? valor
        : '/'
}

self.addEventListener('notificationclick', (evento) => {
    evento.notification.close()
    const caminho = caminhoInterno(evento.notification.data && evento.notification.data.caminho)
    evento.waitUntil(
        self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((janelas) => {
            const aberta = janelas.find((j) => new URL(j.url).origin === self.location.origin)
            if (aberta) return aberta.focus().then((j) => (j ? j.navigate(caminho) : undefined))
            return self.clients.openWindow(caminho)
        }),
    )
})
