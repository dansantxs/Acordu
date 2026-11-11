---
description: Escreve a spec de um marco (o quê) a partir do template
argument-hint: <marco> <descrição curta>
---

Escreva a spec do marco **$ARGUMENTS** seguindo `docs/processo.md` §3.2.

1. Leia `CLAUDE.md`, `docs/processo.md`, `docs/specs/_template/spec.md` e os documentos que o marco toca (ERS, ADRs, contrato da API, modelo de domínio, segurança). Leia o código existente da área para saber o que já está pronto. Se houver issue do marco, leia (`gh issue view N`).
2. Crie `docs/specs/<marco>-<nome-curto>/spec.md` com status `Rascunho`, a partir do template e pelas regras do processo §3.2. Só o quê: o resto vai no `design.md` (`/design`).
3. Dúvida de requisito vai para o Daniel; não invente.
4. Acrescente a linha do marco em `docs/specs/README.md` e rode `node scripts/conferir-specs.mjs`.
5. Siga para o `/design` do mesmo marco: spec e design vão juntos no PR (portão G2).
