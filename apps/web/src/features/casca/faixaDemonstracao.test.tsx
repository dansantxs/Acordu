import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { Tenant } from '@/shared/sessao/gravacao'
import { perfil } from '@/testes/app'
import { FaixaSituacao } from './FaixaSituacao'

// Spec do F14, F14s (RF-NEB-02, F14-89): a empresa de demonstração mostra a faixa dela, e não a do teste.

const AGORA = new Date('2026-11-16T13:00:00Z')

describe('FaixaSituacao: empresa de demonstração', () => {
    it('FaixaDemonstracao_EmpresaDeDemonstracao_DizQueNadaSaiPorEmail', () => {
        const tenant: Tenant = { ...perfil.tenant, situacao: 'ATIVA', fimDoTeste: null, demonstracao: true }
        render(<FaixaSituacao tenant={tenant} agora={AGORA} />)
        const faixa = screen.getByRole('status')
        expect(faixa).toHaveTextContent('Empresa de demonstração.')
        expect(faixa).toHaveTextContent(/nada sai daqui por e-mail/)
    })

    it('FaixaDemonstracao_EmpresaComum_NaoAparece', () => {
        const tenant: Tenant = { ...perfil.tenant, situacao: 'ATIVA', fimDoTeste: null, demonstracao: false }
        render(<FaixaSituacao tenant={tenant} agora={AGORA} />)
        expect(screen.queryByRole('status')).not.toBeInTheDocument()
    })
})
