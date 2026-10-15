import '@testing-library/jest-dom/vitest'
import { configure } from '@testing-library/react'

// O primeiro teste de tela de cada arquivo paga a carga das rotas; 1 s (o padrão) é pouco numa máquina de CI lenta.
configure({ asyncUtilTimeout: 5000 })
