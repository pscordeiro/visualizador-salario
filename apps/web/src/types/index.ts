// Auth types
export interface User {
  id: string
  email: string
  nome?: string
}

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  user: User
}

// Arquivo types
export interface ArquivoPdf {
  id: string
  nomeOriginal: string
  tamanhoBytes: number
  status: 'pendente' | 'processando' | 'processado' | 'erro'
  erroMensagem?: string
  createdAt: string
  processedAt?: string
  holeritesCount: number
  ano?: number
  mes?: number
  tipo?: string
  referencia?: string
}

export interface UploadResult {
  arquivo: string
  arquivoId?: string
  sucesso: boolean
  erro?: string
}

// Holerite types
export interface Holerite {
  id: number
  userId?: string
  arquivoId?: string
  ano: number
  mes: number
  tipo: string
  empresa: string
  cnpj: string
  cargo: string
  salarioBase: number
  totalVencimentos: number
  totalDescontos: number
  valorLiquido: number
  baseInss: number
  baseFgts: number
  fgtsMes: number
  baseIrrf: number
  faixaIrrf: number
  arquivoOrigem: string
}

export interface Rubrica {
  id: number
  holeriteId: number
  codigo: string
  descricao: string
  referencia: string
  vencimento: number
  desconto: number
}

export interface ResumoAnual {
  ano: number
  totalLiquidoMensal: number
  total13: number
  totalFerias: number
  total14: number
  totalGeral: number
  totalDescontos: number
  totalFgts: number
}

export interface EvolucaoSalarial {
  ano: number
  mes: number
  periodo: string
  salarioBase: number
  valorLiquido: number
  totalVencimentos: number
  totalDescontos: number
}

export interface Estatisticas {
  salarioAtual: number
  totalRecebidoAnoAtual: number
  totalImpostosAnoAtual: number
  fgtsAcumulado: number
  mediaMensalAnoAtual: number
  variacaoSalarial: number
  totalHolerites: number
}

export interface ResumoImpostos {
  ano: number
  totalInss: number
  totalIrrf: number
  totalImpostos: number
}

export interface Beneficio {
  ano: number
  descricao: string
  total: number
}

export interface ImpostoMensal {
  ano: number
  mes: number
  tipo: string
  periodo: string
  inss: number
  irrf: number
  total: number
  bruto: number
  liquido: number
}

// ECharts tooltip params (apenas os campos que usamos)
export interface ChartTooltipParam {
  name: string
  seriesName: string
  data: number
  color: string
}

// Tooltip de pie chart (trigger: 'item') usa parâmetro único
export interface ChartItemTooltipParam {
  name: string
  value: number
  percent: number
}
