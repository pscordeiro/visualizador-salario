<script setup lang="ts">
import { computed, defineAsyncComponent, onMounted, ref } from 'vue'
import { useApi } from '@/composables/useApi'
import { formatCurrencyOrHidden } from '@/composables/useHoleriteFormat'
import Card from '@/components/ui/Card.vue'
import StatsCard from '@/components/dashboard/StatsCard.vue'
import DashboardHeader from '@/components/dashboard/DashboardHeader.vue'
import ResumoAnualTable from '@/components/dashboard/ResumoAnualTable.vue'
import HoleritesList from '@/components/dashboard/HoleritesList.vue'
import ImpostosList from '@/components/dashboard/ImpostosList.vue'
import ValoresOcultos from '@/components/dashboard/ValoresOcultos.vue'
import type {
  Estatisticas,
  EvolucaoSalarial,
  Holerite,
  ImpostoMensal,
  ResumoAnual,
  ResumoImpostos,
} from '@/types'

// Charts são lazy-loaded — ECharts é pesado (~470 kB) e só precisa quando o dashboard renderiza
const EvolucaoChart = defineAsyncComponent(() => import('@/components/charts/EvolucaoChart.vue'))
const BrutoLiquidoChart = defineAsyncComponent(() => import('@/components/charts/BrutoLiquidoChart.vue'))
const ImpostosChart = defineAsyncComponent(() => import('@/components/charts/ImpostosChart.vue'))
const ResumoAnualChart = defineAsyncComponent(() => import('@/components/charts/ResumoAnualChart.vue'))

const api = useApi()

const estatisticas = ref<Estatisticas | null>(null)
const evolucao = ref<EvolucaoSalarial[]>([])
const resumoAnual = ref<ResumoAnual[]>([])
const impostos = ref<ResumoImpostos[]>([])
const impostosMensal = ref<ImpostoMensal[]>([])
const holerites = ref<Holerite[]>([])
const loading = ref(true)

const valoresVisiveis = ref(true)
const tabAtiva = ref<'holerites' | 'impostos'>('holerites')

const formatCurrency = (value: number) => formatCurrencyOrHidden(value, valoresVisiveis.value)

const totalImpostosAno = computed(() =>
  impostos.value.reduce((acc, i) => acc + i.totalImpostos, 0),
)

const anosDisponiveis = computed(() =>
  [...new Set(holerites.value.map((h) => h.ano))].sort((a, b) => b - a),
)

const anoAtual = computed(() => {
  if (resumoAnual.value.length === 0) return new Date().getFullYear()
  return Math.max(...resumoAnual.value.map((r) => r.ano))
})

onMounted(async () => {
  loading.value = true
  const [stats, evol, resumo, imp, hols, impMensal] = await Promise.all([
    api.getEstatisticas(),
    api.getEvolucaoSalarial(),
    api.getResumoAnual(),
    api.getResumoImpostos(),
    api.getHolerites(),
    api.getImpostosMensal(),
  ])
  estatisticas.value = stats
  evolucao.value = evol
  resumoAnual.value = resumo
  impostos.value = imp
  holerites.value = hols
  impostosMensal.value = impMensal
  loading.value = false
})
</script>

<template>
  <div class="min-h-screen bg-background">
    <DashboardHeader
      :valores-visiveis="valoresVisiveis"
      :ano-atual="anoAtual"
      :total-anos="anosDisponiveis.length"
      :total-holerites="holerites.length"
      @toggle-valores="valoresVisiveis = !valoresVisiveis"
    />

    <main class="container mx-auto px-6 py-8">
      <div v-if="loading" class="flex items-center justify-center h-64">
        <div class="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
      </div>

      <template v-else>
        <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-8">
          <StatsCard
            title="Salário Atual"
            :value="formatCurrency(estatisticas?.salarioAtual ?? 0)"
            subtitle="Base mensal"
            :trend="valoresVisiveis ? estatisticas?.variacaoSalarial : undefined"
          />
          <StatsCard
            title="Total Recebido"
            :value="formatCurrency(estatisticas?.totalRecebidoAnoAtual ?? 0)"
            :subtitle="`Em ${anoAtual}`"
          />
          <StatsCard
            title="Impostos Pagos"
            :value="formatCurrency(estatisticas?.totalImpostosAnoAtual ?? 0)"
            :subtitle="`INSS + IRRF em ${anoAtual}`"
          />
          <StatsCard
            title="FGTS Acumulado"
            :value="formatCurrency(estatisticas?.fgtsAcumulado ?? 0)"
            subtitle="Total depositado"
          />
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
          <Card class="p-6">
            <h3 class="text-lg font-semibold mb-4">Evolução do Salário Base</h3>
            <EvolucaoChart v-if="valoresVisiveis" :data="evolucao" />
            <ValoresOcultos v-else />
          </Card>
          <Card class="p-6">
            <h3 class="text-lg font-semibold mb-4">Bruto vs Líquido</h3>
            <BrutoLiquidoChart v-if="valoresVisiveis" :data="evolucao" />
            <ValoresOcultos v-else />
          </Card>
        </div>

        <div class="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-6">
          <Card class="p-6 lg:col-span-2">
            <h3 class="text-lg font-semibold mb-4">Resumo por Ano</h3>
            <ResumoAnualChart v-if="valoresVisiveis" :data="resumoAnual" />
            <ValoresOcultos v-else />
          </Card>
          <Card class="p-6">
            <h3 class="text-lg font-semibold mb-4">Distribuição de Impostos</h3>
            <template v-if="valoresVisiveis">
              <ImpostosChart :data="impostos" />
              <div class="mt-4 text-center">
                <p class="text-sm text-muted-foreground">Total pago</p>
                <p class="text-xl font-bold text-primary">{{ formatCurrency(totalImpostosAno) }}</p>
              </div>
            </template>
            <ValoresOcultos v-else />
          </Card>
        </div>

        <ResumoAnualTable :data="resumoAnual" :format-currency="formatCurrency" />

        <div class="flex gap-2 mb-6">
          <button
            type="button"
            :class="[
              'px-4 py-2 rounded-lg text-sm font-medium transition-colors',
              tabAtiva === 'holerites'
                ? 'bg-primary text-primary-foreground'
                : 'bg-card hover:bg-card/80 text-muted-foreground',
            ]"
            @click="tabAtiva = 'holerites'"
          >
            Holerites Detalhados
          </button>
          <button
            type="button"
            :class="[
              'px-4 py-2 rounded-lg text-sm font-medium transition-colors',
              tabAtiva === 'impostos'
                ? 'bg-primary text-primary-foreground'
                : 'bg-card hover:bg-card/80 text-muted-foreground',
            ]"
            @click="tabAtiva = 'impostos'"
          >
            Impostos Detalhados
          </button>
        </div>

        <HoleritesList
          v-if="tabAtiva === 'holerites'"
          :holerites="holerites"
          :format-currency="formatCurrency"
        />
        <ImpostosList
          v-else
          :impostos="impostosMensal"
          :format-currency="formatCurrency"
        />
      </template>
    </main>
  </div>
</template>
