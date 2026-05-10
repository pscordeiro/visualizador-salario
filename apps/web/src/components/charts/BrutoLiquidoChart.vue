<script setup lang="ts">
import { computed } from 'vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { BarChart } from 'echarts/charts'
import {
  TooltipComponent,
  LegendComponent,
  GridComponent
} from 'echarts/components'
import type { ChartTooltipParam, EvolucaoSalarial } from '@/types'
import { formatCurrency } from '@/lib/utils'

use([
  CanvasRenderer,
  BarChart,
  TooltipComponent,
  LegendComponent,
  GridComponent
])

interface Props {
  data: EvolucaoSalarial[]
}

const props = defineProps<Props>()

const option = computed(() => ({
  backgroundColor: 'transparent',
  tooltip: {
    trigger: 'axis',
    backgroundColor: '#18181b',
    borderColor: '#27272a',
    textStyle: { color: '#fafafa' },
    formatter: (params: ChartTooltipParam[]) => {
      if (params.length === 0) return ''
      return `
        <div class="text-sm">
          <div class="font-semibold mb-1">${params[0]!.name}</div>
          ${params.map((p) => `<div>${p.seriesName}: ${formatCurrency(p.data)}</div>`).join('')}
        </div>
      `
    }
  },
  legend: {
    data: ['Bruto', 'Líquido'],
    textStyle: { color: '#a1a1aa' },
    top: 0
  },
  grid: {
    left: '3%',
    right: '4%',
    bottom: '3%',
    top: '15%',
    containLabel: true
  },
  xAxis: {
    type: 'category',
    data: props.data.map(d => d.periodo),
    axisLine: { lineStyle: { color: '#27272a' } },
    axisLabel: { color: '#a1a1aa', fontSize: 10, rotate: 45 },
    splitLine: { show: false }
  },
  yAxis: {
    type: 'value',
    axisLine: { show: false },
    axisLabel: {
      color: '#a1a1aa',
      fontSize: 11,
      formatter: (value: number) => `R$ ${(value / 1000).toFixed(1)}k`
    },
    splitLine: { lineStyle: { color: '#27272a', type: 'dashed' } }
  },
  series: [
    {
      name: 'Bruto',
      type: 'bar',
      data: props.data.map(d => d.totalVencimentos),
      itemStyle: { color: '#6366f1', borderRadius: [4, 4, 0, 0] },
      barWidth: '35%'
    },
    {
      name: 'Líquido',
      type: 'bar',
      data: props.data.map(d => d.valorLiquido),
      itemStyle: { color: '#22c55e', borderRadius: [4, 4, 0, 0] },
      barWidth: '35%'
    }
  ]
}))
</script>

<template>
  <VChart :option="option" autoresize style="height: 300px" />
</template>
