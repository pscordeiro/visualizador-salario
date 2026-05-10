<script setup lang="ts">
import { computed } from 'vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { CanvasRenderer } from 'echarts/renderers'
import { PieChart } from 'echarts/charts'
import {
  TooltipComponent,
  LegendComponent
} from 'echarts/components'
import type { ChartItemTooltipParam, ResumoImpostos } from '@/types'
import { formatCurrency } from '@/lib/utils'

use([
  CanvasRenderer,
  PieChart,
  TooltipComponent,
  LegendComponent
])

interface Props {
  data: ResumoImpostos[]
}

const props = defineProps<Props>()

const totalByType = computed(() => {
  const totals = { inss: 0, irrf: 0 }
  props.data.forEach(d => {
    totals.inss += d.totalInss
    totals.irrf += d.totalIrrf
  })
  return totals
})

const option = computed(() => ({
  backgroundColor: 'transparent',
  tooltip: {
    trigger: 'item',
    backgroundColor: '#18181b',
    borderColor: '#27272a',
    textStyle: { color: '#fafafa' },
    formatter: (params: ChartItemTooltipParam) => {
      return `${params.name}: ${formatCurrency(params.value)} (${params.percent}%)`
    }
  },
  legend: {
    orient: 'vertical',
    right: '5%',
    top: 'center',
    textStyle: { color: '#a1a1aa' }
  },
  series: [
    {
      type: 'pie',
      radius: ['45%', '70%'],
      center: ['35%', '50%'],
      avoidLabelOverlap: false,
      itemStyle: {
        borderRadius: 8,
        borderColor: '#09090b',
        borderWidth: 2
      },
      label: { show: false },
      emphasis: {
        label: {
          show: true,
          fontSize: 14,
          fontWeight: 'bold',
          color: '#fafafa'
        }
      },
      labelLine: { show: false },
      data: [
        { value: totalByType.value.inss, name: 'INSS', itemStyle: { color: '#f59e0b' } },
        { value: totalByType.value.irrf, name: 'IRRF', itemStyle: { color: '#ef4444' } }
      ]
    }
  ]
}))
</script>

<template>
  <VChart :option="option" autoresize style="height: 250px" />
</template>
