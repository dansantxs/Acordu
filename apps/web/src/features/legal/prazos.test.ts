import { describe, expect, it } from 'vitest'
import { TEXTOS } from './textosLegais'

// Só os fatos objetivos que o produto sustenta (prazos e canal); o conteúdo jurídico é revisão manual (F14-85/86).
const corpo = (tipo: 'termos' | 'privacidade') => TEXTOS[tipo].corpo.replace(/\*\*/g, '')

describe('textos legais: prazos e canal que o produto sustenta', () => {
    it('Privacidade_Retencao_InformaTesteExpurgoBackupETrilha', () => {
        const texto = corpo('privacidade')
        expect(texto).toMatch(/teste dura 30 dias/)
        expect(texto).toMatch(/só leitura por 30 dias/)
        expect(texto).toMatch(/guardada por 14 dias/)
        expect(texto).toMatch(/Trilha de auditoria:\s*12 meses/)
        expect(texto).toContain('contato@acordu.app.br')
    })

    it('Termos_Prazos_InformaTesteExpurgoEBackup', () => {
        const texto = corpo('termos')
        expect(texto).toMatch(/período de teste de 30 dias/)
        expect(texto).toMatch(/Passados 30 dias desde o início do só leitura/)
        expect(texto).toMatch(/guardadas por 14 dias/)
        expect(texto).toContain('contato@acordu.app.br')
    })
})
