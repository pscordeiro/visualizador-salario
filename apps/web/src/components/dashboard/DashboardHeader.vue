<script setup lang="ts">
import { Eye, EyeOff, FilePlus, LogOut } from 'lucide-vue-next'
import { useRouter } from 'vue-router'
import { useAuth } from '@/composables/useAuth'

interface Props {
  valoresVisiveis: boolean
  anoAtual: number
  totalAnos: number
  totalHolerites: number
}

defineProps<Props>()

const emit = defineEmits<{
  'toggle-valores': []
}>()

const router = useRouter()
const { user, logout } = useAuth()

async function handleLogout() {
  await logout()
  router.push('/login')
}
</script>

<template>
  <header class="border-b border-border bg-card/50 backdrop-blur-sm sticky top-0 z-10">
    <div class="container mx-auto px-6 py-4">
      <div class="flex items-center justify-between">
        <router-link
          to="/"
          class="group focus:outline-none focus-visible:ring-2 focus-visible:ring-primary rounded-md"
          aria-label="Voltar para o início"
        >
          <h1 class="text-2xl font-bold tracking-tight group-hover:text-primary transition-colors">
            Visualizador de Salário
          </h1>
          <p class="text-sm text-muted-foreground">Análise de holerites e evolução salarial</p>
        </router-link>

        <div class="flex items-center gap-4">
          <button
            type="button"
            class="px-4 py-2 text-sm font-medium text-zinc-300 hover:text-white hover:bg-zinc-800 rounded-lg transition-colors flex items-center gap-2"
            @click="router.push('/arquivos')"
          >
            <FilePlus class="w-4 h-4" />
            Meus Arquivos
          </button>

          <div class="flex items-center gap-2">
            <span class="text-sm text-muted-foreground">{{ user?.email || `${totalHolerites} holerites` }}</span>
            <span class="text-muted-foreground">•</span>
            <span class="text-sm text-muted-foreground">{{ totalAnos }} anos</span>
          </div>

          <span class="px-3 py-1 bg-primary/10 text-primary rounded-md text-sm font-medium">
            {{ anoAtual }}
          </span>

          <button
            type="button"
            class="p-2 rounded-lg hover:bg-card transition-colors"
            :title="valoresVisiveis ? 'Ocultar valores' : 'Mostrar valores'"
            @click="emit('toggle-valores')"
          >
            <Eye v-if="valoresVisiveis" class="w-5 h-5 text-muted-foreground" />
            <EyeOff v-else class="w-5 h-5 text-muted-foreground" />
          </button>

          <button
            type="button"
            class="p-2 rounded-lg hover:bg-red-500/10 transition-colors text-muted-foreground hover:text-red-400"
            title="Sair"
            @click="handleLogout"
          >
            <LogOut class="w-5 h-5" />
          </button>
        </div>
      </div>
    </div>
  </header>
</template>
