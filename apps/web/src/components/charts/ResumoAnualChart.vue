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
import type { ChartTooltipParam, ResumoAnual } from '@/types'
import { formatCurrency } from '@/lib/utils'

use([
  CanvasRenderer,
  BarChart,
  TooltipComponent,
  LegendComponent,
  GridComponent
])

interface Props {
  data: ResumoAnual[]
}

const props = defineProps<Props>()

const option = computed(() => ({
  backgroundColor: 'transparent',
  tooltip: {
    trigger: 'axis',
    backgroundColor: '#18181b',
    borderColor: '#27272a',
    textStyle: { color: '#fafafa' },
    axisPointer: { type: 'shadow' },
    formatter: (params: ChartTooltipParam[]) => {
      if (params.length === 0) return ''
      const total = params.reduce((acc, p) => acc + (p.data || 0), 0)
      return `
        <div class="text-sm">
          <div class="font-semibold mb-2">${params[0]!.name}</div>
          ${params.filter((p) => p.data > 0).map((p) => `<div style="display:flex;align-items:center;gap:6px;margin-bottom:2px"><span style="width:10px;height:10px;border-radius:2px;background:${p.color}"></span>${p.seriesName}: ${formatCurrency(p.data)}</div>`).join('')}
          <div style="border-top:1px solid #3f3f46;margin-top:6px;padding-top:6px;font-weight:600">Total: ${formatCurrency(total)}</div>
        </div>
      `
    }
  },
  legend: {
    data: ['Mensal', '13º Salário', 'Férias', '14º/PLR'],
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
    data: props.data.map(d => d.ano.toString()),
    axisLine: { lineStyle: { color: '#27272a' } },
    axisLabel: { color: '#a1a1aa', fontSize: 12 },
    splitLine: { show: false }
  },
  yAxis: {
    type: 'value',
    axisLine: { show: false },
    axisLabel: {
      color: '#a1a1aa',
      fontSize: 11,
      formatter: (value: number) => `R$ ${(value / 1000).toFixed(0)}k`
    },
    splitLine: { lineStyle: { color: '#27272a', type: 'dashed' } }
  },
  series: [
    {
      name: 'Mensal',
      type: 'bar',
      stack: 'total',
      data: props.data.map(d => d.totalLiquidoMensal),
      itemStyle: { color: '#6366f1', borderRadius: [0, 0, 0, 0] }
    },
    {
      name: '13º Salário',
      type: 'bar',
      stack: 'total',
      data: props.data.map(d => d.total13),
      itemStyle: { color: '#22c55e' }
    },
    {
      name: 'Férias',
      type: 'bar',
      stack: 'total',
      data: props.data.map(d => d.totalFerias),
      itemStyle: { color: '#f59e0b' }
    },
    {
      name: '14º/PLR',
      type: 'bar',
      stack: 'total',
      data: props.data.map(d => d.total14 || 0),
      itemStyle: { color: '#ec4899', borderRadius: [4, 4, 0, 0] }
    }
  ]
}))
</script>

<template>
  <VChart :option="option" autoresize style="height: 300px" />
</template>
