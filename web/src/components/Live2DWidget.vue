<template>
  <div class="live2d-container" ref="containerRef">
    <canvas
      ref="canvasRef"
      class="live2d-canvas"
      :class="{ visible: !loading }"
      @pointerdown.stop
      @pointermove.stop
      @pointerup.stop
    ></canvas>
    <div class="loading" v-if="loading">
      <div class="progress-ring">
        <svg viewBox="0 0 36 36">
          <circle class="track" cx="18" cy="18" r="16" />
          <circle class="fill" cx="18" cy="18" r="16"
            :stroke-dasharray="`${progress * 100.53} 100.53`"
          />
        </svg>
        <span class="progress-text">{{ Math.round(progress * 100) }}%</span>
      </div>
      <span class="loading-label">{{ loadingLabel }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from 'vue'

const containerRef = ref<HTMLElement>()
const canvasRef = ref<HTMLCanvasElement>()
const loading = ref(true)
const progress = ref(0)
const loadingLabel = ref('加载核心...')

let cleanupCalled = false

const audioExtensions = ['.mp3', '.wav', '.ogg', '.m4a', '.aac']

function isAudioUrl(url: string): boolean {
  return audioExtensions.some(ext => url.toLowerCase().endsWith(ext))
}

function patchFetchForProgress(): () => void {
  const originalFetch = window.fetch

  let loadedCount = 0
  let knownTotal = 0
  const counted = new Set<string>()

  const maxRetries = 3
  const retryDelay = 500

  async function fetchWithRetry(url: string, init?: RequestInit): Promise<Response> {
    let lastError: Error | null = null
    for (let attempt = 0; attempt <= maxRetries; attempt++) {
      try {
        const response = await originalFetch(url, init)
        if (response.ok) return response
        lastError = new Error(`HTTP ${response.status}`)
      } catch (err) {
        lastError = err as Error
      }
      if (attempt < maxRetries) {
        await new Promise(r => setTimeout(r, retryDelay * (attempt + 1)))
      }
    }
    console.warn(`[Live2D] Failed to load after ${maxRetries} retries: ${url}`)
    return new Response(new ArrayBuffer(0), { status: 404, statusText: 'Not Found' })
  }

  window.fetch = function (input: RequestInfo | URL, init?: RequestInit): Promise<Response> {
    let url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url

    if (url.includes('#')) {
      url = url.replace(/#/g, '%23')
      input = url
    }

    // Skip audio files
    if (isAudioUrl(url)) {
      return originalFetch.call(this, input, init)
    }

    // Track model resources
    if (url.includes('/model/')) {
      // Parse model3.json to count total resources
      if (url.endsWith('.model3.json')) {
        return fetchWithRetry(url, init).then(async response => {
          if (!counted.has(url)) {
            counted.add(url)
            loadedCount++
            try {
              const json = await response.clone().json()
              let total = 2
              if (json.FileReferences?.Textures) total += json.FileReferences.Textures.length
              if (json.FileReferences?.Expressions) total += json.FileReferences.Expressions.length
              if (json.FileReferences?.Physics) total++
              if (json.FileReferences?.Motions) {
                for (const entries of Object.values(json.FileReferences.Motions)) {
                  for (const e of entries as any[]) {
                    if (e.File && e.File !== 'NullValue') total++
                  }
                }
              }
              knownTotal = total
            } catch {}
            progress.value = Math.min(loadedCount / Math.max(knownTotal, 1), 0.99)
          }
          return response
        })
      }

      // All other model resources: retry + track progress
      return fetchWithRetry(url, init).then(response => {
        if (!counted.has(url)) {
          counted.add(url)
          loadedCount++
          if (knownTotal > 0) {
            progress.value = Math.min(loadedCount / knownTotal, 0.99)
          }
        }
        return response
      })
    }

    return originalFetch.call(this, input, init)
  }

  // Return cleanup function
  return () => {
    window.fetch = originalFetch
  }
}

async function loadCubismCore(): Promise<void> {
  return new Promise((resolve, reject) => {
    const script = document.createElement('script')
    script.src = '/Core/live2dcubismcore.min.js'
    script.onload = () => resolve()
    script.onerror = () => reject(new Error('Failed to load Cubism Core'))
    document.head.appendChild(script)
  })
}

onMounted(async () => {
  const canvas = canvasRef.value
  if (!canvas) return

  const restoreFetch = patchFetchForProgress()

  try {
    loadingLabel.value = '加载核心...'
    progress.value = 0.02

    await loadCubismCore()

    if (typeof (window as any).Live2DCubismCore === 'undefined') {
      throw new Error('Live2DCubismCore not found after loading script')
    }

    const core = (window as any).Live2DCubismCore
    if (core.ColorBlendType_Normal === undefined) {
      core.ColorBlendType_Normal = 0
      core.ColorBlendType_AddGlow = 1
      core.ColorBlendType_Add = 2
      core.ColorBlendType_Darken = 3
      core.ColorBlendType_Multiply = 4
      core.ColorBlendType_ColorBurn = 5
      core.ColorBlendType_LinearBurn = 6
      core.ColorBlendType_Lighten = 7
      core.ColorBlendType_Screen = 8
      core.ColorBlendType_ColorDodge = 9
      core.ColorBlendType_Overlay = 10
      core.ColorBlendType_SoftLight = 11
      core.ColorBlendType_HardLight = 12
      core.ColorBlendType_LinearLight = 13
      core.ColorBlendType_Hue = 14
      core.ColorBlendType_Color = 15
      core.ColorBlendType_AddCompatible = 16
      core.ColorBlendType_MultiplyCompatible = 17
    }

    loadingLabel.value = '加载模型...'
    progress.value = 0.05

    const { LAppDelegate } = await import('../live2d/lappdelegate')

    const delegate = LAppDelegate.getInstance()
    if (!delegate.initialize(canvas)) {
      throw new Error('Failed to initialize LAppDelegate')
    }

    // Wait for model to be fully loaded
    loadingLabel.value = '加载资源...'
    await new Promise<void>(resolve => {
      const check = setInterval(() => {
        const manager = delegate.getLive2DManager?.()
        if (manager) {
          const model = manager.getModel?.()
          if (model && model.isLoaded()) {
            clearInterval(check)
            resolve()
          }
        }
      }, 50)
      // Timeout after 30s
      setTimeout(() => { clearInterval(check); resolve() }, 30000)
    })

    progress.value = 1
    await new Promise(r => setTimeout(r, 300)) // Brief pause for smooth transition

    delegate.run()
    loading.value = false
  } catch (err) {
    console.error('[Live2D] Initialization failed:', err)
    loading.value = false
  } finally {
    restoreFetch()
  }
})

onUnmounted(() => {
  if (cleanupCalled) return
  cleanupCalled = true

  import('../live2d/lappdelegate').then(({ LAppDelegate }) => {
    LAppDelegate.releaseInstance()
  })
})
</script>

<style scoped>
.live2d-container {
  position: relative;
  display: flex;
  justify-content: center;
  align-items: center;
  width: 100%;
  max-width: 600px;
  aspect-ratio: 4 / 3;
}

.live2d-canvas {
  width: 100%;
  height: 100%;
  border-radius: var(--radius-lg);
  opacity: 0;
  transition: opacity 0.6s var(--ease-out);
}

.live2d-canvas.visible {
  opacity: 1;
}

.loading {
  position: absolute;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
  color: var(--color-text-secondary);
  font-size: 13px;
  font-weight: 500;
}

.progress-ring {
  position: relative;
  width: 56px;
  height: 56px;
}

.progress-ring svg {
  width: 100%;
  height: 100%;
  transform: rotate(-90deg);
}

.progress-ring .track {
  fill: none;
  stroke: var(--color-border);
  stroke-width: 3;
}

.progress-ring .fill {
  fill: none;
  stroke: var(--color-primary);
  stroke-width: 3;
  stroke-linecap: round;
  transition: stroke-dasharray 0.3s var(--ease-out);
}

.progress-text {
  position: absolute;
  inset: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 11px;
  font-weight: 600;
  color: var(--color-primary);
  font-family: var(--font-mono);
}

.loading-label {
  font-size: 12px;
  color: var(--color-text-secondary);
  letter-spacing: 0.02em;
}
</style>
