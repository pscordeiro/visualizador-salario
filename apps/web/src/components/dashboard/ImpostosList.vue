<script setup lang="ts">
import { computed, ref } from 'vue'
import Card from '@/components/ui/Card.vue'
import { mesNome, tipoColor, tipoLabel, tipoOrdem } from '@/composables/useHoleriteFormat'
import type { ImpostoMensal } from '@/types'

type Ordenacao = 'data' | 'inss' | 'irrf' | 'total'
type Ordem = 'asc' | 'desc'

interface Props {
  impostos: ImpostoMensal[]
  formatCurrency: (value: number) => string
}

const props = defineProps<Props>()

const anoFiltro = ref<number | 'todos'>('todos')
const tipoFiltro = ref<string>('todos')
const ordenacao = ref<Ordenacao>('data')
const ordem = ref<Ordem>('desc')

const anosDisponiveis = computed(() =>
  [...new Set(props.impostos.map((i) => i.ano))].sort((a, b) => b - a),
)

const tiposDisponiveis = computed(() =>
  [...new Set(props.impostos.map((i) => i.tipo))].sort(),
)

const impostosFiltrados = computed(() => {
  const lista = props.impostos.filter((i) => {
    if (anoFiltro.value !== 'todos' && i.ano !== anoFiltro.value) return false
    if (tipoFiltro.value !== 'todos' && i.tipo !== tipoFiltro.value) return false
    return true
  })

  lista.sort((a, b) => {
    let diff = 0
    switch (ordenacao.value) {
      case 'data':
        diff = a.ano * 100 + a.mes - (b.ano * 100 + b.mes)
        if (diff === 0) diff = tipoOrdem(a.tipo) - tipoOrdem(b.tipo)
        break
      case 'inss':
        diff = a.inss - b.inss
        break
      case 'irrf':
        diff = a.irrf - b.irrf
        break
      case 'total':
        diff = a.total - b.total
        break
    }
    return ordem.value === 'desc' ? -diff : diff
  })

  return lista
})

const totais = computed(() =>
  impostosFiltrados.value.reduce(
    (acc, i) => ({ inss: acc.inss + i.inss, irrf: acc.irrf + i.irrf, total: acc.total + i.total }),
    { inss: 0, irrf: 0, total: 0 },
  ),
)

function toggleOrdenacao(campo: Ordenacao) {
  if (ordenacao.value === campo) {
    ordem.value = ordem.value === 'desc' ? 'asc' : 'desc'
  } else {
    ordenacao.value = campo
    ordem.value = 'desc'
  }
}
</script>

<template>
  <Card class="p-6">
    <div class="flex items-center justify-between mb-6">
      <h3 class="text-lg font-semibold">Impostos por Competência</h3>
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
        <span class="text-sm text-muted-foreground">{{ impostosFiltrados.length }} resultado(s)</span>
      </div>
    </div>

    <div class="grid grid-cols-3 gap-4 mb-6">
      <div class="bg-blue-500/10 rounded-lg p-4 text-center">
        <p class="text-sm text-blue-400">Total INSS</p>
        <p class="text-xl font-bold text-blue-400">{{ formatCurrency(totais.inss) }}</p>
      </div>
      <div class="bg-orange-500/10 rounded-lg p-4 text-center">
        <p class="text-sm text-orange-400">Total IRRF</p>
        <p class="text-xl font-bold text-orange-400">{{ formatCurrency(totais.irrf) }}</p>
      </div>
      <div class="bg-red-500/10 rounded-lg p-4 text-center">
        <p class="text-sm text-red-400">Total Impostos</p>
        <p class="text-xl font-bold text-red-400">{{ formatCurrency(totais.total) }}</p>
      </div>
    </div>

    <div class="overflow-x-auto">
      <table class="w-full">
        <thead>
          <tr class="border-b border-border">
            <th
              class="text-left py-3 px-4 text-sm font-medium text-muted-foreground cursor-pointer hover:text-foreground"
              @click="toggleOrdenacao('data')"
            >
              Competência
              <span v-if="ordenacao === 'data'" class="ml-1">{{ ordem === 'desc' ? '↓' : '↑' }}</span>
            </th>
            <th class="text-left py-3 px-4 text-sm font-medium text-muted-foreground">Tipo</th>
            <th class="text-right py-3 px-4 text-sm font-medium text-muted-foreground">Bruto</th>
            <th
              class="text-right py-3 px-4 text-sm font-medium text-muted-foreground cursor-pointer hover:text-foreground"
              @click="toggleOrdenacao('inss')"
            >
              INSS
              <span v-if="ordenacao === 'inss'" class="ml-1">{{ ordem === 'desc' ? '↓' : '↑' }}</span>
            </th>
            <th
              class="text-right py-3 px-4 text-sm font-medium text-muted-foreground cursor-pointer hover:text-foreground"
              @click="toggleOrdenacao('irrf')"
            >
              IRRF
              <span v-if="ordenacao === 'irrf'" class="ml-1">{{ ordem === 'desc' ? '↓' : '↑' }}</span>
            </th>
            <th
              class="text-right py-3 px-4 text-sm font-medium text-muted-foreground cursor-pointer hover:text-foreground"
              @click="toggleOrdenacao('total')"
            >
              Total
              <span v-if="ordenacao === 'total'" class="ml-1">{{ ordem === 'desc' ? '↓' : '↑' }}</span>
            </th>
            <th class="text-right py-3 px-4 text-sm font-medium text-muted-foreground">% Carga</th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="(imp, idx) in impostosFiltrados"
            :key="idx"
            class="border-b border-border/50 hover:bg-card"
          >
            <td class="py-3 px-4">
              <span class="font-medium">{{ mesNome(imp.mes) }}/{{ imp.ano }}</span>
            </td>
            <td class="py-3 px-4">
              <span :class="['px-2 py-1 rounded-md text-xs font-medium', tipoColor(imp.tipo)]">
                {{ tipoLabel(imp.tipo) }}
              </span>
            </td>
            <td class="py-3 px-4 text-right">{{ formatCurrency(imp.bruto) }}</td>
            <td class="py-3 px-4 text-right text-blue-400">{{ formatCurrency(imp.inss) }}</td>
            <td class="py-3 px-4 text-right text-orange-400">{{ formatCurrency(imp.irrf) }}</td>
            <td class="py-3 px-4 text-right font-semibold text-red-400">{{ formatCurrency(imp.total) }}</td>
            <td class="py-3 px-4 text-right text-muted-foreground">
              {{ imp.bruto > 0 ? ((imp.total / imp.bruto) * 100).toFixed(1) : 0 }}%
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </Card>
</template>
