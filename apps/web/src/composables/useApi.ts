import { ref } from 'vue'
import { useAuth } from './useAuth'
import type {
  ArquivoPdf,
  Beneficio,
  Estatisticas,
  EvolucaoSalarial,
  Holerite,
  ImpostoMensal,
  ResumoAnual,
  ResumoImpostos,
  Rubrica,
  UploadResult,
} from '@/types'

export function useApi() {
  const auth = useAuth()
  const loading = ref(false)
  const error = ref<string | null>(null)

  async function authedFetch(path: string, init: RequestInit = {}): Promise<Response> {
    const headers = new Headers(init.headers ?? {})
    const token = auth.getToken()
    if (token) headers.set('Authorization', `Bearer ${token}`)

    let response = await fetch(`${auth.apiBase}${path}`, { ...init, headers })

    // Tenta refresh uma vez em 401
    if (response.status === 401 && auth.getRefreshToken()) {
      const ok = await auth.refresh()
      if (ok) {
        const newToken = auth.getToken()
        if (newToken) headers.set('Authorization', `Bearer ${newToken}`)
        response = await fetch(`${auth.apiBase}${path}`, { ...init, headers })
      }
    }

    if (response.status === 401) {
      await auth.logout()
      window.location.href = '/login'
      throw new Error('Sessão expirada')
    }
    return response
  }

  async function getJson<T>(path: string): Promise<T | null> {
    loading.value = true
    error.value = null
    try {
      const response = await authedFetch(path)
      if (!response.ok) {
        error.value = `HTTP ${response.status}`
        return null
      }
      return (await response.json()) as T
    } catch (e) {
      error.value = (e as Error).message
      return null
    } finally {
      loading.value = false
    }
  }

  async function getList<T>(path: string): Promise<T[]> {
    return (await getJson<T[]>(path)) ?? []
  }

  return {
    loading,
    error,

    getHolerites: () => getList<Holerite>('/holerites'),
    getHoleritesByAno: (ano: number) => getList<Holerite>(`/holerites/${ano}`),
    getRubricas: (holeriteId: number) => getList<Rubrica>(`/rubricas/${holeriteId}`),
    getResumoAnual: () => getList<ResumoAnual>('/resumo/anual'),
    getResumoImpostos: () => getList<ResumoImpostos>('/resumo/impostos'),
    getEvolucaoSalarial: () => getList<EvolucaoSalarial>('/evolucao/salario'),
    getEstatisticas: () => getJson<Estatisticas>('/estatisticas'),
    getBeneficios: () => getList<Beneficio>('/beneficios'),
    getImpostosMensal: () => getList<ImpostoMensal>('/impostos/mensal'),
    getArquivos: () => getList<ArquivoPdf>('/arquivos'),

    async uploadArquivos(files: File[]): Promise<UploadResult[]> {
      loading.value = true
      error.value = null
      try {
        const formData = new FormData()
        files.forEach((file) => formData.append('files', file))
        const response = await authedFetch('/arquivos/upload', { method: 'POST', body: formData })
        // O endpoint pode retornar 200, 207 (parcial) ou 400 (todos falharam) — todos trazem `resultados`
        const data = await response.json().catch(() => null)
        return (data as { resultados?: UploadResult[] } | null)?.resultados ?? []
      } catch (e) {
        error.value = (e as Error).message
        return []
      } finally {
        loading.value = false
      }
    },

    async deleteArquivo(id: string): Promise<boolean> {
      loading.value = true
      error.value = null
      try {
        const response = await authedFetch(`/arquivos/${id}`, { method: 'DELETE' })
        return response.ok
      } catch (e) {
        error.value = (e as Error).message
        return false
      } finally {
        loading.value = false
      }
    },

    async deleteAllArquivos(): Promise<{ success: boolean; count: number }> {
      loading.value = true
      error.value = null
      try {
        const response = await authedFetch('/arquivos', { method: 'DELETE' })
        if (!response.ok) return { success: false, count: 0 }
        const data = (await response.json()) as { count: number }
        return { success: true, count: data.count }
      } catch (e) {
        error.value = (e as Error).message
        return { success: false, count: 0 }
      } finally {
        loading.value = false
      }
    },

    async reprocessarArquivo(id: string): Promise<boolean> {
      loading.value = true
      error.value = null
      try {
        const response = await authedFetch(`/arquivos/${id}/reprocessar`, { method: 'POST' })
        return response.ok
      } catch (e) {
        error.value = (e as Error).message
        return false
      } finally {
        loading.value = false
      }
    },
  }
}
