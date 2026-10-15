import { createFileRoute, Outlet } from '@tanstack/react-router'

/** Telas públicas de acesso (entrar, criar conta). Cada uma decide se quem já tem sessão segue direto. */
export const Route = createFileRoute('/_acesso')({ component: Outlet })
