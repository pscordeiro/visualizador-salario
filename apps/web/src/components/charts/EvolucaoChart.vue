<script setup lang="ts">
import { computed } from 'vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { LineChart } from 'echarts/charts'
import {
  TitleComponent,
  TooltipComponent,
  LegendComponent,
  GridComponent
} from 'echarts/components'
import type { ChartTooltipParam, EvolucaoSalarial } from '@/types'
import { formatCurrency } from '@/lib/utils'

use([
  CanvasRenderer,
  LineChart,
  TitleComponent,
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
      const data = params[0]
      if (!data) return ''
      return `
        <div class="text-sm">
          <div class="font-semibold mb-1">${data.name}</div>
          <div>Salário Base: ${formatCurrency(data.data)}</div>
        </div>
      `
    }
  },
  grid: {
    left: '3%',
    right: '4%',
    bottom: '3%',
    top: '10%',
    containLabel: true
  },
  xAxis: {
    type: 'category',
    boundaryGap: false,
    data: props.data.map(d => d.periodo),
    axisLine: { lineStyle: { color: '#27272a' } },
    axisLabel: { color: '#a1a1aa', fontSize: 11 },
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
      name: 'Salário Base',
      type: 'line',
      smooth: true,
      symbol: 'circle',
      symbolSize: 6,
      data: props.data.map(d => d.salarioBase),
      lineStyle: { color: '#6366f1', width: 3 },
      itemStyle: { color: '#6366f1' },
      areaStyle: {
        color: {
          type: 'linear',
          x: 0, y: 0, x2: 0, y2: 1,
          colorStops: [
            { offset: 0, color: 'rgba(99, 102, 241, 0.3)' },
            { offset: 1, color: 'rgba(99, 102, 241, 0.05)' }
          ]
        }
      }
    }
  ]
}))
</script>

<template>
  <VChart :option="option" autoresize style="height: 300px" />
</template>
