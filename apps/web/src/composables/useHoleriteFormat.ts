import { formatCurrency as formatCurrencyUtil } from '@/lib/utils'

const TIPO_LABEL: Record<string, string> = {
  MENSAL: 'Mensal',
  '13A': '13º Adiant.',
  '13I': '13º Integral',
  FERIAS: 'Férias',
  '14S': '14º/PLR',
}

const TIPO_COLOR: Record<string, string> = {
  MENSAL: 'bg-indigo-500/20 text-indigo-400',
  '13A': 'bg-emerald-500/20 text-emerald-400',
  '13I': 'bg-emerald-500/20 text-emerald-400',
  FERIAS: 'bg-amber-500/20 text-amber-400',
  '14S': 'bg-pink-500/20 text-pink-400',
}

const TIPO_ORDEM: Record<string, number> = {
  MENSAL: 1,
  FERIAS: 2,
  '13A': 3,
  '13I': 4,
  '14S': 5,
}

const MESES_ABREV = ['Jan', 'Fev', 'Mar', 'Abr', 'Mai', 'Jun', 'Jul', 'Ago', 'Set', 'Out', 'Nov', 'Dez']

export function tipoLabel(tipo: string): string {
  return TIPO_LABEL[tipo] ?? tipo
}

export function tipoColor(tipo: string): string {
  return TIPO_COLOR[tipo] ?? 'bg-zinc-500/20 text-zinc-400'
}

export function tipoOrdem(tipo: string): number {
  return TIPO_ORDEM[tipo] ?? 99
}

export function mesNome(mes: number): string {
  return MESES_ABREV[mes - 1] ?? String(mes)
}

/** Formata valor em BRL ou esconde com '•••••' se valores estão ocultos. */
export function formatCurrencyOrHidden(value: number, visible: boolean): string {
  return visible ? formatCurrencyUtil(value) : 'R$ •••••'
}
