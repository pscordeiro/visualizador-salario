import { computed, ref } from 'vue'
import type { AuthResponse, User } from '@/types'

const API_BASE = (import.meta.env.VITE_API_URL as string | undefined) ?? '/api'
const STORAGE_TOKEN = 'accessToken'
const STORAGE_REFRESH = 'refreshToken'
const STORAGE_USER = 'user'

const accessToken = ref<string | null>(null)
const refreshToken = ref<string | null>(null)
const user = ref<User | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)

let initialized = false
function ensureInitialized() {
  if (initialized || typeof window === 'undefined') return
  initialized = true
  const storedToken = localStorage.getItem(STORAGE_TOKEN)
  const storedRefresh = localStorage.getItem(STORAGE_REFRESH)
  const storedUser = localStorage.getItem(STORAGE_USER)
  if (storedToken && storedUser) {
    accessToken.value = storedToken
    refreshToken.value = storedRefresh
    try {
      user.value = JSON.parse(storedUser)
    } catch {
      user.value = null
    }
  }
}

function persist(auth: AuthResponse) {
  accessToken.value = auth.accessToken
  refreshToken.value = auth.refreshToken
  user.value = auth.user
  localStorage.setItem(STORAGE_TOKEN, auth.accessToken)
  localStorage.setItem(STORAGE_REFRESH, auth.refreshToken)
  localStorage.setItem(STORAGE_USER, JSON.stringify(auth.user))
}

function clear() {
  accessToken.value = null
  refreshToken.value = null
  user.value = null
  localStorage.removeItem(STORAGE_TOKEN)
  localStorage.removeItem(STORAGE_REFRESH)
  localStorage.removeItem(STORAGE_USER)
}

async function postJson<T>(
  path: string,
  body: unknown,
): Promise<{ ok: boolean; data: T | null; error: string | null }> {
  try {
    const response = await fetch(`${API_BASE}${path}`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    })
    const data = response.status !== 204 ? await response.json().catch(() => null) : null
    if (!response.ok) {
      return {
        ok: false,
        data: null,
        error: (data as { error?: string } | null)?.error ?? `HTTP ${response.status}`,
      }
    }
    return { ok: true, data: data as T, error: null }
  } catch (e) {
    return { ok: false, data: null, error: (e as Error).message }
  }
}

ensureInitialized()

export function useAuth() {
  ensureInitialized()

  const isAuthenticated = computed(() => !!accessToken.value)

  async function register(email: string, senha: string, nome?: string): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      const result = await postJson<AuthResponse>('/auth/register', { email, senha, nome })
      if (!result.ok || !result.data) {
        error.value = result.error
        return false
      }
      persist(result.data)
      return true
    } finally {
      loading.value = false
    }
  }

  async function login(email: string, senha: string): Promise<boolean> {
    loading.value = true
    error.value = null
    try {
      const result = await postJson<AuthResponse>('/auth/login', { email, senha })
      if (!result.ok || !result.data) {
        error.value = result.error
        return false
      }
      persist(result.data)
      return true
    } finally {
      loading.value = false
    }
  }

  async function refresh(): Promise<boolean> {
    if (!refreshToken.value) return false
    const result = await postJson<AuthResponse>('/auth/refresh', {
      refreshToken: refreshToken.value,
    })
    if (!result.ok || !result.data) {
      clear()
      return false
    }
    persist(result.data)
    return true
  }

  async function logout(): Promise<void> {
    if (refreshToken.value) {
      await postJson<void>('/auth/logout', { refreshToken: refreshToken.value })
    }
    clear()
  }

  return {
    user,
    accessToken,
    isAuthenticated,
    loading,
    error,
    register,
    login,
    logout,
    refresh,
    getToken: () => accessToken.value,
    getRefreshToken: () => refreshToken.value,
    apiBase: API_BASE,
  }
}
