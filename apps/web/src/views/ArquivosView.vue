<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useApi } from '@/composables/useApi'
import { useAuth } from '@/composables/useAuth'
import type { ArquivoPdf, UploadResult } from '@/types'
import {
  Upload,
  Trash2,
  RefreshCw,
  FileText,
  CheckCircle,
  XCircle,
  Clock,
  ArrowLeft,
  LogOut,
  User,
  Calendar,
  AlertTriangle,
  X
} from 'lucide-vue-next'

const router = useRouter()
const { user, logout } = useAuth()
const { getArquivos, uploadArquivos, deleteArquivo, deleteAllArquivos, reprocessarArquivo, loading } = useApi()

const arquivos = ref<ArquivoPdf[]>([])
const uploadResults = ref<UploadResult[]>([])
const showResults = ref(false)
const isDragging = ref(false)
const fileInput = ref<HTMLInputElement | null>(null)
const selectedYear = ref<number | null>(null)
const showDeleteAllModal = ref(false)
const showErrorModal = ref(false)
const errorModalContent = ref<{ arquivo: string; erro: string }[]>([])

onMounted(async () => {
  await loadArquivos()
})

async function loadArquivos() {
  arquivos.value = await getArquivos()
  // Seleciona o ano mais recente por padrão
  if (anos.value.length > 0 && selectedYear.value == null) {
    selectedYear.value = anos.value[0] ?? null
  }
}

// Extrai anos únicos dos arquivos
const anos = computed(() => {
  const anosSet = new Set<number>()
  arquivos.value.forEach(a => {
    if (a.ano) anosSet.add(a.ano)
  })
  return Array.from(anosSet).sort((a, b) => b - a) // Mais recente primeiro
})

// Arquivos filtrados pelo ano selecionado
const arquivosFiltrados = computed(() => {
  if (!selectedYear.value) return arquivos.value
  return arquivos.value.filter(a => a.ano === selectedYear.value || (!a.ano && a.status !== 'processado'))
})

// Agrupa arquivos por tipo dentro do ano
const arquivosPorTipo = computed(() => {
  const grupos: Record<string, ArquivoPdf[]> = {
    'MENSAL': [],
    'FERIAS': [],
    '13A': [],
    '13I': [],
    '14S': [],
    'outros': []
  }

  arquivosFiltrados.value.forEach(a => {
    const grupo = (a.tipo && grupos[a.tipo]) ? grupos[a.tipo] : grupos.outros
    grupo!.push(a)
  })

  // Ordena cada grupo por mês (decrescente)
  for (const lista of Object.values(grupos)) {
    lista.sort((a, b) => (b.mes ?? 0) - (a.mes ?? 0))
  }

  return grupos
})

const tipoLabels: Record<string, string> = {
  'MENSAL': 'Mensais',
  'FERIAS': 'Férias',
  '13A': '13º Adiantamento',
  '13I': '13º Integral',
  '14S': '14º Salário',
  'outros': 'Outros'
}

function handleDragOver(e: DragEvent) {
  e.preventDefault()
  isDragging.value = true
}

function handleDragLeave() {
  isDragging.value = false
}

async function handleDrop(e: DragEvent) {
  e.preventDefault()
  isDragging.value = false

  const files = Array.from(e.dataTransfer?.files || []).filter(f => f.type === 'application/pdf')
  if (files.length > 0) {
    await handleUpload(files)
  }
}

function triggerFileInput() {
  fileInput.value?.click()
}

async function handleFileSelect(e: Event) {
  const target = e.target as HTMLInputElement
  const files = Array.from(target.files || [])
  if (files.length > 0) {
    await handleUpload(files)
  }
  target.value = ''
}

async function handleUpload(files: File[]) {
  uploadResults.value = await uploadArquivos(files)

  // Verifica se há erros de duplicata
  const erros = uploadResults.value.filter(r => !r.sucesso && r.erro)
  if (erros.length > 0) {
    errorModalContent.value = erros.map(e => ({ arquivo: e.arquivo, erro: e.erro || 'Erro desconhecido' }))
    showErrorModal.value = true
  }

  showResults.value = true
  await loadArquivos()
}

async function handleDelete(id: string) {
  if (confirm('Tem certeza que deseja deletar este arquivo? Os holerites associados também serão removidos.')) {
    const success = await deleteArquivo(id)
    if (success) {
      await loadArquivos()
    }
  }
}

async function handleDeleteAll() {
  const result = await deleteAllArquivos()
  if (result.success) {
    showDeleteAllModal.value = false
    selectedYear.value = null
    await loadArquivos()
  }
}

async function handleReprocess(id: string) {
  const success = await reprocessarArquivo(id)
  if (success) {
    await loadArquivos()
  }
}

async function handleLogout() {
  await logout()
  router.push('/login')
}

function formatBytes(bytes: number): string {
  if (bytes < 1024) return bytes + ' B'
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(1) + ' KB'
  return (bytes / (1024 * 1024)).toFixed(1) + ' MB'
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleString('pt-BR')
}

const statusConfig = {
  pendente: { icon: Clock, color: 'text-yellow-400', bg: 'bg-yellow-500/10', label: 'Pendente' },
  processando: { icon: RefreshCw, color: 'text-blue-400', bg: 'bg-blue-500/10', label: 'Processando' },
  processado: { icon: CheckCircle, color: 'text-green-400', bg: 'bg-green-500/10', label: 'Processado' },
  erro: { icon: XCircle, color: 'text-red-400', bg: 'bg-red-500/10', label: 'Erro' }
}

const totalHolerites = computed(() =>
  arquivos.value.reduce((sum, a) => sum + a.holeritesCount, 0)
)
</script>

<template>
  <div class="min-h-screen bg-zinc-950 text-white">
    <!-- Header -->
    <header class="border-b border-zinc-800 bg-zinc-900/50 backdrop-blur-sm sticky top-0 z-10">
      <div class="max-w-6xl mx-auto px-4 py-4">
        <div class="flex items-center justify-between">
          <div class="flex items-center gap-4">
            <button
              @click="router.push('/')"
              class="p-2 text-zinc-400 hover:text-white hover:bg-zinc-800 rounded-lg transition-colors"
              title="Voltar ao Dashboard"
            >
              <ArrowLeft class="w-5 h-5" />
            </button>
            <div>
              <h1 class="text-xl font-bold">Meus Arquivos</h1>
              <p class="text-sm text-zinc-400">{{ arquivos.length }} arquivos | {{ totalHolerites }} holerites</p>
            </div>
          </div>

          <div class="flex items-center gap-3">
            <!-- User Info -->
            <div class="flex items-center gap-2 px-3 py-2 bg-zinc-800/50 rounded-lg">
              <User class="w-4 h-4 text-zinc-400" />
              <span class="text-sm text-zinc-300">{{ user?.nome || user?.email }}</span>
            </div>

            <!-- Delete All Button -->
            <button
              v-if="arquivos.length > 0"
              @click="showDeleteAllModal = true"
              class="flex items-center gap-2 px-3 py-2 text-sm text-red-400 hover:text-red-300 hover:bg-red-500/10 rounded-lg transition-colors"
              title="Apagar todos os arquivos"
            >
              <Trash2 class="w-4 h-4" />
              <span class="hidden sm:inline">Apagar Todos</span>
            </button>

            <!-- Logout Button -->
            <button
              @click="handleLogout"
              class="flex items-center gap-2 px-3 py-2 text-zinc-400 hover:text-white hover:bg-zinc-800 rounded-lg transition-colors"
              title="Sair"
            >
              <LogOut class="w-4 h-4" />
              <span class="hidden sm:inline">Sair</span>
            </button>
          </div>
        </div>
      </div>
    </header>

    <main class="max-w-6xl mx-auto px-4 py-8">
      <!-- Year Pills -->
      <div v-if="anos.length > 0" class="flex items-center gap-4 mb-6">
        <div class="flex items-center gap-2 text-zinc-400">
          <Calendar class="w-5 h-5" />
          <span class="text-sm font-medium">Anos:</span>
        </div>
        <div class="flex flex-wrap gap-2">
          <button
            v-for="ano in anos"
            :key="ano"
            @click="selectedYear = ano"
            :class="[
              'px-4 py-2 rounded-full text-sm font-medium transition-all',
              selectedYear === ano
                ? 'bg-indigo-500 text-white'
                : 'bg-zinc-800 text-zinc-400 hover:bg-zinc-700 hover:text-white'
            ]"
          >
            {{ ano }}
          </button>
          <button
            @click="selectedYear = null"
            :class="[
              'px-4 py-2 rounded-full text-sm font-medium transition-all',
              selectedYear === null
                ? 'bg-indigo-500 text-white'
                : 'bg-zinc-800 text-zinc-400 hover:bg-zinc-700 hover:text-white'
            ]"
          >
            Todos
          </button>
        </div>
      </div>

      <!-- Upload Zone -->
      <div
        @dragover="handleDragOver"
        @dragleave="handleDragLeave"
        @drop="handleDrop"
        @click="triggerFileInput"
        :class="[
          'border-2 border-dashed rounded-xl p-8 text-center cursor-pointer transition-all mb-8',
          isDragging
            ? 'border-indigo-500 bg-indigo-500/10'
            : 'border-zinc-700 hover:border-zinc-600 hover:bg-zinc-900/50'
        ]"
      >
        <input
          ref="fileInput"
          type="file"
          accept=".pdf"
          multiple
          class="hidden"
          @change="handleFileSelect"
        />
        <Upload :class="['w-10 h-10 mx-auto mb-3', isDragging ? 'text-indigo-400' : 'text-zinc-500']" />
        <p class="text-zinc-300 mb-1">
          Arraste arquivos PDF aqui ou clique para selecionar
        </p>
        <p class="text-sm text-zinc-500">
          Os holerites serão processados automaticamente após o upload
        </p>
      </div>

      <!-- Upload Results -->
      <div v-if="showResults && uploadResults.length > 0" class="mb-8 p-4 bg-zinc-900 border border-zinc-800 rounded-xl">
        <div class="flex items-center justify-between mb-4">
          <h3 class="font-semibold">Resultado do Upload</h3>
          <button @click="showResults = false" class="text-zinc-400 hover:text-white text-xl">&times;</button>
        </div>
        <div class="space-y-2">
          <div
            v-for="result in uploadResults"
            :key="result.arquivo"
            :class="[
              'flex items-center gap-3 p-3 rounded-lg',
              result.sucesso ? 'bg-green-500/10' : 'bg-red-500/10'
            ]"
          >
            <component
              :is="result.sucesso ? CheckCircle : XCircle"
              :class="['w-5 h-5 shrink-0', result.sucesso ? 'text-green-400' : 'text-red-400']"
            />
            <span class="flex-1 truncate">{{ result.arquivo }}</span>
            <span v-if="result.erro" class="text-sm text-red-400 shrink-0">{{ result.erro }}</span>
          </div>
        </div>
      </div>

      <!-- Loading -->
      <div v-if="loading" class="text-center py-12">
        <RefreshCw class="w-8 h-8 mx-auto text-indigo-400 animate-spin mb-4" />
        <p class="text-zinc-400">Carregando...</p>
      </div>

      <!-- Empty State -->
      <div v-else-if="arquivos.length === 0" class="text-center py-12">
        <FileText class="w-16 h-16 mx-auto text-zinc-600 mb-4" />
        <p class="text-xl text-zinc-400 mb-2">Nenhum arquivo enviado</p>
        <p class="text-zinc-500">Faça upload de seus holerites em PDF para começar</p>
      </div>

      <!-- File List by Type -->
      <div v-else class="space-y-8">
        <template v-for="(tipo, key) in arquivosPorTipo" :key="key">
          <div v-if="tipo.length > 0">
            <h3 class="text-lg font-semibold text-zinc-300 mb-4 flex items-center gap-2">
              <span class="w-2 h-2 rounded-full bg-indigo-500"></span>
              {{ tipoLabels[key] || key }}
              <span class="text-sm text-zinc-500 font-normal">({{ tipo.length }})</span>
            </h3>

            <div class="grid gap-3">
              <div
                v-for="arquivo in tipo"
                :key="arquivo.id"
                class="bg-zinc-900 border border-zinc-800 rounded-xl p-4 hover:border-zinc-700 transition-colors"
              >
                <div class="flex items-start gap-4">
                  <div :class="['p-3 rounded-lg shrink-0', statusConfig[arquivo.status].bg]">
                    <FileText :class="['w-5 h-5', statusConfig[arquivo.status].color]" />
                  </div>

                  <div class="flex-1 min-w-0">
                    <div class="flex items-center gap-2 mb-1">
                      <span v-if="arquivo.referencia" class="text-sm font-medium text-indigo-400">
                        {{ arquivo.referencia }}
                      </span>
                      <h4 class="font-medium truncate text-zinc-200">{{ arquivo.nomeOriginal }}</h4>
                    </div>

                    <div class="flex items-center flex-wrap gap-3 text-sm text-zinc-400">
                      <span
                        :class="[
                          'px-2 py-0.5 text-xs rounded-full',
                          statusConfig[arquivo.status].bg,
                          statusConfig[arquivo.status].color
                        ]"
                      >
                        {{ statusConfig[arquivo.status].label }}
                      </span>
                      <span>{{ formatBytes(arquivo.tamanhoBytes) }}</span>
                      <span>{{ formatDate(arquivo.createdAt) }}</span>
                    </div>

                    <p v-if="arquivo.erroMensagem" class="mt-2 text-sm text-red-400 flex items-start gap-2">
                      <AlertTriangle class="w-4 h-4 shrink-0 mt-0.5" />
                      {{ arquivo.erroMensagem }}
                    </p>
                  </div>

                  <div class="flex items-center gap-1 shrink-0">
                    <button
                      @click="handleReprocess(arquivo.id)"
                      class="p-2 text-zinc-400 hover:text-white hover:bg-zinc-800 rounded-lg transition-colors"
                      title="Reprocessar"
                    >
                      <RefreshCw class="w-4 h-4" />
                    </button>
                    <button
                      @click="handleDelete(arquivo.id)"
                      class="p-2 text-zinc-400 hover:text-red-400 hover:bg-red-500/10 rounded-lg transition-colors"
                      title="Deletar"
                    >
                      <Trash2 class="w-4 h-4" />
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </template>
      </div>
    </main>

    <!-- Delete All Modal -->
    <Teleport to="body">
      <div
        v-if="showDeleteAllModal"
        class="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center z-50 p-4"
        @click.self="showDeleteAllModal = false"
      >
        <div class="bg-zinc-900 border border-zinc-800 rounded-xl p-6 max-w-md w-full">
          <div class="flex items-center gap-3 text-red-400 mb-4">
            <AlertTriangle class="w-6 h-6" />
            <h3 class="text-lg font-semibold">Apagar Todos os Arquivos</h3>
          </div>

          <p class="text-zinc-300 mb-2">
            Tem certeza que deseja apagar <strong>todos os {{ arquivos.length }} arquivos</strong>?
          </p>
          <p class="text-zinc-400 text-sm mb-6">
            Esta ação não pode ser desfeita. Todos os holerites e dados associados serão removidos permanentemente.
          </p>

          <div class="flex justify-end gap-3">
            <button
              @click="showDeleteAllModal = false"
              class="px-4 py-2 text-zinc-400 hover:text-white hover:bg-zinc-800 rounded-lg transition-colors"
            >
              Cancelar
            </button>
            <button
              @click="handleDeleteAll"
              class="px-4 py-2 bg-red-500 hover:bg-red-600 text-white rounded-lg transition-colors"
            >
              Apagar Todos
            </button>
          </div>
        </div>
      </div>
    </Teleport>

    <!-- Error Modal -->
    <Teleport to="body">
      <div
        v-if="showErrorModal"
        class="fixed inset-0 bg-black/70 backdrop-blur-sm flex items-center justify-center z-50 p-4"
        @click.self="showErrorModal = false"
      >
        <div class="bg-zinc-900 border border-zinc-800 rounded-xl p-6 max-w-lg w-full">
          <div class="flex items-center justify-between mb-4">
            <div class="flex items-center gap-3 text-red-400">
              <XCircle class="w-6 h-6" />
              <h3 class="text-lg font-semibold">Erros no Upload</h3>
            </div>
            <button
              @click="showErrorModal = false"
              class="p-1 text-zinc-400 hover:text-white hover:bg-zinc-800 rounded transition-colors"
            >
              <X class="w-5 h-5" />
            </button>
          </div>

          <p class="text-zinc-400 text-sm mb-4">
            Alguns arquivos não puderam ser processados:
          </p>

          <div class="space-y-3 max-h-60 overflow-y-auto">
            <div
              v-for="(item, index) in errorModalContent"
              :key="index"
              class="p-3 bg-red-500/10 border border-red-500/20 rounded-lg"
            >
              <p class="font-medium text-zinc-200 truncate">{{ item.arquivo }}</p>
              <p class="text-sm text-red-400 mt-1">{{ item.erro }}</p>
            </div>
          </div>

          <div class="flex justify-end mt-6">
            <button
              @click="showErrorModal = false"
              class="px-4 py-2 bg-zinc-800 hover:bg-zinc-700 text-white rounded-lg transition-colors"
            >
              Entendi
            </button>
          </div>
        </div>
      </div>
    </Teleport>
  </div>
</template>
