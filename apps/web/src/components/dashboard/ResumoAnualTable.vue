<script setup lang="ts">
import Card from '@/components/ui/Card.vue'
import type { ResumoAnual } from '@/types'

interface Props {
  data: ResumoAnual[]
  formatCurrency: (value: number) => string
}

defineProps<Props>()
</script>

<template>
  <Card class="p-6 mb-6">
    <h3 class="text-lg font-semibold mb-4">Resumo por Ano</h3>
    <div class="overflow-x-auto">
      <table class="w-full">
        <thead>
          <tr class="border-b border-border">
            <th class="text-left py-3 px-4 text-sm font-medium text-muted-foreground">Ano</th>
            <th class="text-right py-3 px-4 text-sm font-medium text-muted-foreground">Mensal</th>
            <th class="text-right py-3 px-4 text-sm font-medium text-muted-foreground">13º Salário</th>
            <th class="text-right py-3 px-4 text-sm font-medium text-muted-foreground">Férias</th>
            <th class="text-right py-3 px-4 text-sm font-medium text-muted-foreground">14º/PLR</th>
            <th class="text-right py-3 px-4 text-sm font-medium text-muted-foreground">Total Geral</th>
            <th class="text-right py-3 px-4 text-sm font-medium text-muted-foreground">FGTS</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="ano in data" :key="ano.ano" class="border-b border-border/50 hover:bg-card">
            <td class="py-3 px-4 font-medium">{{ ano.ano }}</td>
            <td class="py-3 px-4 text-right">{{ formatCurrency(ano.totalLiquidoMensal) }}</td>
            <td class="py-3 px-4 text-right">{{ formatCurrency(ano.total13) }}</td>
            <td class="py-3 px-4 text-right">{{ formatCurrency(ano.totalFerias) }}</td>
            <td class="py-3 px-4 text-right">{{ formatCurrency(ano.total14 || 0) }}</td>
            <td class="py-3 px-4 text-right font-semibold text-primary">{{ formatCurrency(ano.totalGeral) }}</td>
            <td class="py-3 px-4 text-right text-success">{{ formatCurrency(ano.totalFgts) }}</td>
          </tr>
        </tbody>
      </table>
    </div>
  </Card>
</template>
