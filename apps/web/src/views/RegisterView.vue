<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '@/composables/useAuth'

const router = useRouter()
const { register, loading, error } = useAuth()

const nome = ref('')
const email = ref('')
const senha = ref('')
const confirmarSenha = ref('')
const localError = ref('')

async function handleSubmit() {
  localError.value = ''

  if (senha.value !== confirmarSenha.value) {
    localError.value = 'As senhas não coincidem'
    return
  }

  if (senha.value.length < 6) {
    localError.value = 'A senha deve ter no mínimo 6 caracteres'
    return
  }

  const success = await register(email.value, senha.value, nome.value || undefined)
  if (success) {
    router.push('/')
  }
}
</script>

<template>
  <div class="min-h-screen bg-zinc-950 flex items-center justify-center p-4">
    <div class="w-full max-w-md">
      <div class="bg-zinc-900 border border-zinc-800 rounded-xl p-8">
        <h1 class="text-2xl font-bold text-white text-center mb-2">
          Criar Conta
        </h1>
        <p class="text-zinc-400 text-center mb-8">
          Preencha os dados abaixo para se registrar
        </p>

        <form @submit.prevent="handleSubmit" class="space-y-4">
          <div>
            <label for="nome" class="block text-sm font-medium text-zinc-300 mb-1">
              Nome (opcional)
            </label>
            <input
              id="nome"
              v-model="nome"
              type="text"
              class="w-full px-4 py-3 bg-zinc-800 border border-zinc-700 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              placeholder="Seu nome"
            />
          </div>

          <div>
            <label for="email" class="block text-sm font-medium text-zinc-300 mb-1">
              Email
            </label>
            <input
              id="email"
              v-model="email"
              type="email"
              required
              class="w-full px-4 py-3 bg-zinc-800 border border-zinc-700 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              placeholder="seu@email.com"
            />
          </div>

          <div>
            <label for="senha" class="block text-sm font-medium text-zinc-300 mb-1">
              Senha
            </label>
            <input
              id="senha"
              v-model="senha"
              type="password"
              required
              class="w-full px-4 py-3 bg-zinc-800 border border-zinc-700 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              placeholder="Mínimo 6 caracteres"
            />
          </div>

          <div>
            <label for="confirmarSenha" class="block text-sm font-medium text-zinc-300 mb-1">
              Confirmar Senha
            </label>
            <input
              id="confirmarSenha"
              v-model="confirmarSenha"
              type="password"
              required
              class="w-full px-4 py-3 bg-zinc-800 border border-zinc-700 rounded-lg text-white placeholder-zinc-500 focus:outline-none focus:ring-2 focus:ring-indigo-500 focus:border-transparent"
              placeholder="Repita a senha"
            />
          </div>

          <div v-if="error || localError" class="p-3 bg-red-500/10 border border-red-500/20 rounded-lg">
            <p class="text-red-400 text-sm">{{ localError || error }}</p>
          </div>

          <button
            type="submit"
            :disabled="loading"
            class="w-full py-3 px-4 bg-indigo-600 hover:bg-indigo-700 disabled:opacity-50 disabled:cursor-not-allowed text-white font-medium rounded-lg transition-colors"
          >
            {{ loading ? 'Registrando...' : 'Criar Conta' }}
          </button>
        </form>

        <p class="text-zinc-400 text-center mt-6">
          Já tem uma conta?
          <router-link to="/login" class="text-indigo-400 hover:text-indigo-300">
            Faça login
          </router-link>
        </p>
      </div>
    </div>
  </div>
</template>
