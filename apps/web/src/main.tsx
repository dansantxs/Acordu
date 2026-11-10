import '@fontsource-variable/fraunces'
import '@fontsource-variable/source-sans-3'
import './app/estilos.css'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { Aplicacao } from './app/Aplicacao'
import { prepararInstalacao } from './shared/app/instalacao'

prepararInstalacao()

const raiz = document.getElementById('raiz')
if (!raiz) throw new Error('Elemento #raiz não encontrado.')

createRoot(raiz).render(
    <StrictMode>
        <Aplicacao />
    </StrictMode>,
)
