<script setup lang="ts">
import { EyeOff } from 'lucide-vue-next'

interface Props {
  height?: string
}

withDefaults(defineProps<Props>(), { height: '300px' })
</script>

<template>
  <div
    class="flex items-center justify-center bg-card/50 rounded-lg border border-border/50"
    :style="{ height }"
  >
    <div class="text-center text-muted-foreground">
      <EyeOff class="w-12 h-12 mx-auto mb-2 opacity-50" />
      <p>Valores ocultos</p>
    </div>
  </div>
</template>
