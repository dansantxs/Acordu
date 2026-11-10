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
    // Só a navegação passa por aqui; o resto (API, arquivos do app) segue direto, sem cache do service worker.
    if (evento.request.mode !== 'navigate') return
    evento.respondWith(fetch(evento.request).catch(() => caches.match('/offline.html', { cacheName: VERSAO })))
})

self.addEventListener('notificationclick', (evento) => {
    evento.notification.close()
    const caminho = (evento.notification.data && evento.notification.data.caminho) || '/'
    evento.waitUntil(
        self.clients.matchAll({ type: 'window', includeUncontrolled: true }).then((janelas) => {
            const aberta = janelas.find((j) => new URL(j.url).origin === self.location.origin)
            if (aberta) return aberta.focus().then((j) => (j ? j.navigate(caminho) : undefined))
            return self.clients.openWindow(caminho)
        }),
    )
})
