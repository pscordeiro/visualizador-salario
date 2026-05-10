<script setup lang="ts">
import { ArrowDown, ArrowUp } from 'lucide-vue-next'
import Card from '@/components/ui/Card.vue'
import { cn } from '@/lib/utils'

interface Props {
  title: string
  value: string
  subtitle?: string
  trend?: number
  class?: string
}

const props = defineProps<Props>()
</script>

<template>
  <Card :class="cn('p-6', props.class)">
    <div class="flex items-start justify-between">
      <div class="space-y-1">
        <p class="text-sm font-medium text-muted-foreground">{{ title }}</p>
        <p class="text-2xl font-bold tracking-tight">{{ value }}</p>
        <p v-if="subtitle" class="text-xs text-muted-foreground">{{ subtitle }}</p>
      </div>
      <div v-if="trend !== undefined" class="flex items-center gap-1">
        <span
          :class="[
            'text-sm font-medium',
            trend >= 0 ? 'text-success' : 'text-danger',
          ]"
        >
          {{ trend >= 0 ? '+' : '' }}{{ trend.toFixed(1) }}%
        </span>
        <ArrowUp v-if="trend >= 0" class="h-4 w-4 text-success" />
        <ArrowDown v-else class="h-4 w-4 text-danger" />
      </div>
    </div>
  </Card>
</template>
