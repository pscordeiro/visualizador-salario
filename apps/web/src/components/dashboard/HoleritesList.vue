<script setup lang="ts">
import { computed, ref } from 'vue'
import { ChevronDown } from 'lucide-vue-next'
import Card from '@/components/ui/Card.vue'
import { useApi } from '@/composables/useApi'
import { mesNome, tipoColor, tipoLabel, tipoOrdem } from '@/composables/useHoleriteFormat'
import type { Holerite, Rubrica } from '@/types'

interface Props {
  holerites: Holerite[]
  formatCurrency: (value: number) => string
}

const props = defineProps<Props>()
const api = useApi()

const anoFiltro = ref<number | 'todos'>('todos')
const tipoFiltro = ref<string>('todos')
const holeriteExpandido = ref<number | null>(null)
const rubricasExpandidas = ref<Rubrica[]>([])
const loadingRubricas = ref(false)

const anosDisponiveis = computed(() =>
  [...new Set(props.holerites.map((h) => h.ano))].sort((a, b) => b - a),
)

const tiposDisponiveis = computed(() =>
  [...new Set(props.holerites.map((h) => h.tipo))].sort(),
)

const holeritesFiltrados = computed(() =>
  props.holerites
    .filter((h) => {
      if (anoFiltro.value !== 'todos' && h.ano !== anoFiltro.value) return false
      if (tipoFiltro.value !== 'todos' && h.tipo !== tipoFiltro.value) return false
      return true
    })
    .sort((a, b) => {
      if (b.ano !== a.ano) return b.ano - a.ano
      if (b.mes !== a.mes) return b.mes - a.mes
      return tipoOrdem(a.tipo) - tipoOrdem(b.tipo)
    }),
)

async function expandirHolerite(id: number) {
  if (holeriteExpandido.value === id) {
    holeriteExpandido.value = null
    rubricasExpandidas.value = []
    return
  }
  holeriteExpandido.value = id
  loadingRubricas.value = true
  rubricasExpandidas.value = await api.getRubricas(id)
  loadingRubricas.value = false
}
</script>

<template>
  <Card class="p-6">
    <div class="flex items-center justify-between mb-6">
      <h3 class="text-lg font-semibold">Holerites</h3>
      <div class="flex items-center gap-4">
        <label class="flex items-center gap-2">
          <span class="text-sm text-muted-foreground">Ano:</span>
          <select
            v-model="anoFiltro"
            class="bg-background border border-border rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          >
            <option value="todos">Todos</option>
            <option v-for="ano in anosDisponiveis" :key="ano" :value="ano">{{ ano }}</option>
          </select>
        </label>
        <label class="flex items-center gap-2">
          <span class="text-sm text-muted-foreground">Tipo:</span>
          <select
            v-model="tipoFiltro"
            class="bg-background border border-border rounded-md px-3 py-1.5 text-sm focus:outline-none focus:ring-2 focus:ring-primary"
          >
            <option value="todos">Todos</option>
            <option v-for="tipo in tiposDisponiveis" :key="tipo" :value="tipo">{{ tipoLabel(tipo) }}</option>
          </select>
        </label>
        <span class="text-sm text-muted-foreground">{{ holeritesFiltrados.length }} resultado(s)</span>
      </div>
    </div>

    <div class="space-y-2">
      <div
        v-for="h in holeritesFiltrados"
        :key="h.id"
        class="border border-border rounded-lg overflow-hidden"
      >
        <button
          type="button"
          class="w-full flex items-center justify-between p-4 cursor-pointer hover:bg-card/80 transition-colors text-left"
          @click="expandirHolerite(h.id)"
        >
          <div class="flex items-center gap-4">
            <div class="flex items-center gap-3">
              <span class="text-lg font-semibold w-16">{{ mesNome(h.mes) }}</span>
              <span class="text-muted-foreground">/</span>
              <span class="text-lg">{{ h.ano }}</span>
            </div>
            <span :class="['px-2 py-1 rounded-md text-xs font-medium', tipoColor(h.tipo)]">
              {{ tipoLabel(h.tipo) }}
            </span>
          </div>
          <div class="flex items-center gap-8">
            <div class="text-right">
              <p class="text-xs text-muted-foreground">Bruto</p>
              <p class="font-medium">{{ formatCurrency(h.totalVencimentos) }}</p>
            </div>
            <div class="text-right">
              <p class="text-xs text-muted-foreground">Descontos</p>
              <p class="font-medium text-red-400">-{{ formatCurrency(h.totalDescontos) }}</p>
            </div>
            <div class="text-right min-w-[120px]">
              <p class="text-xs text-muted-foreground">Líquido</p>
              <p class="text-lg font-bold text-primary">{{ formatCurrency(h.valorLiquido) }}</p>
            </div>
            <ChevronDown
              :class="[
                'w-5 h-5 text-muted-foreground transition-transform',
                holeriteExpandido === h.id ? 'rotate-180' : '',
              ]"
            />
          </div>
        </button>

        <div v-if="holeriteExpandido === h.id" class="border-t border-border bg-card/50 p-4">
          <div v-if="loadingRubricas" class="flex justify-center py-4">
            <div class="animate-spin rounded-full h-6 w-6 border-b-2 border-primary"></div>
          </div>
          <div v-else-if="rubricasExpandidas.length === 0" class="text-center py-4 text-muted-foreground">
            Nenhuma rubrica encontrada
          </div>
          <div v-else class="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <h4 class="text-sm font-semibold text-emerald-400 mb-2">Vencimentos</h4>
              <div class="space-y-1">
                <div
                  v-for="r in rubricasExpandidas.filter((r) => r.vencimento > 0)"
                  :key="r.id"
                  class="flex justify-between text-sm py-1 border-b border-border/30"
                >
                  <span class="text-muted-foreground">
                    <span class="text-xs text-zinc-500 mr-2">[{{ r.codigo }}]</span>
                    {{ r.descricao }}
                  </span>
                  <span class="text-emerald-400 font-medium">{{ formatCurrency(r.vencimento) }}</span>
                </div>
              </div>
            </div>
            <div>
              <h4 class="text-sm font-semibold text-red-400 mb-2">Descontos</h4>
              <div class="space-y-1">
                <div
                  v-for="r in rubricasExpandidas.filter((r) => r.desconto > 0)"
                  :key="r.id"
                  class="flex justify-between text-sm py-1 border-b border-border/30"
                >
                  <span class="text-muted-foreground">
                    <span class="text-xs text-zinc-500 mr-2">[{{ r.codigo }}]</span>
                    {{ r.descricao }}
                  </span>
                  <span class="text-red-400 font-medium">-{{ formatCurrency(r.desconto) }}</span>
                </div>
              </div>
            </div>
          </div>
          <div class="mt-4 pt-4 border-t border-border flex flex-wrap gap-6 text-sm">
            <div>
              <span class="text-muted-foreground">Empresa:</span>
              <span class="ml-2 font-medium">{{ h.empresa || 'N/A' }}</span>
            </div>
            <div v-if="h.cargo">
              <span class="text-muted-foreground">Cargo:</span>
              <span class="ml-2 font-medium">{{ h.cargo }}</span>
            </div>
            <div>
              <span class="text-muted-foreground">Salário Base:</span>
              <span class="ml-2 font-medium">{{ formatCurrency(h.salarioBase) }}</span>
            </div>
            <div>
              <span class="text-muted-foreground">FGTS:</span>
              <span class="ml-2 font-medium text-success">{{ formatCurrency(h.fgtsMes) }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  </Card>
</template>
