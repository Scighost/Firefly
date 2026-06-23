<template>
  <section id="hero" class="hero">
    <div class="hero-bg">
      <div class="glow glow-1"></div>
      <div class="glow glow-2"></div>
      <div class="grid-overlay"></div>
    </div>
    <div class="hero-content container">
      <div class="hero-text">
        <div class="hero-badge">
          <span class="badge-dot"></span>
          Live2D Desktop Companion
        </div>
        <h1 class="hero-title">
          <span class="title-line">Firefly</span>
          <span class="title-sub">星穹铁道 · 流萤</span>
        </h1>
        <p class="hero-desc">
          一个基于 Live2D 技术的桌面伴侣，支持表情切换、动作交互与音频播放。
        </p>
        <div class="hero-credits">
          <span class="credits-label">Model by</span>
          <a href="https://space.bilibili.com/457683484" target="_blank" rel="noopener" class="credits-link">bilibili@是依七哒</a>
        </div>
        <div class="hero-actions">
          <a :href="downloadLink" target="_blank" rel="noopener" class="hero-cta primary">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round">
              <path d="M21 15v4a2 2 0 01-2 2H5a2 2 0 01-2-2v-4M7 10l5 5 5-5M12 15V3"/>
            </svg>
            下载
          </a>
          <a href="https://github.com/Scighost/Firefly" target="_blank" rel="noopener noreferrer" class="hero-cta">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
              <path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0024 12c0-6.63-5.37-12-12-12z"/>
            </svg>
            GitHub
          </a>
        </div>
      </div>
      <div class="hero-model">
        <slot></slot>
      </div>
    </div>
  </section>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'

const downloadLink = ref('https://github.com/Scighost/Firefly/releases')

onMounted(async () => {
  try {
    const res = await fetch('https://firefly.scighost.com/release/latest.json')
    const data = await res.json()
    if (data.url) {
      downloadLink.value = data.url
    }
  } catch {
    // fallback already set
  }
})
</script>

<style scoped>
.hero {
  min-height: 100vh;
  display: flex;
  align-items: center;
  position: relative;
  overflow: hidden;
  padding: 48px 0;
}

.hero-bg {
  position: absolute;
  inset: 0;
  pointer-events: none;
  overflow: hidden;
}

.glow {
  position: absolute;
  border-radius: 50%;
  filter: blur(120px);
  opacity: 0.15;
  animation: subtle-drift 20s ease-in-out infinite;
}

.glow-1 {
  width: 600px;
  height: 600px;
  background: var(--color-primary);
  top: 10%;
  left: 20%;
}

.glow-2 {
  width: 400px;
  height: 400px;
  background: var(--color-accent);
  bottom: 10%;
  right: 15%;
  animation-delay: -10s;
}

.grid-overlay {
  position: absolute;
  inset: 0;
  background-image:
    linear-gradient(rgba(120, 220, 160, 0.02) 1px, transparent 1px),
    linear-gradient(90deg, rgba(120, 220, 160, 0.02) 1px, transparent 1px);
  background-size: 60px 60px;
  mask-image: radial-gradient(ellipse 70% 60% at 60% 50%, black 20%, transparent 70%);
  -webkit-mask-image: radial-gradient(ellipse 70% 60% at 60% 50%, black 20%, transparent 70%);
}

.hero-content {
  display: flex;
  align-items: center;
  gap: 48px;
  position: relative;
  z-index: 1;
  width: 100%;
}

.hero-model {
  flex: 2;
  display: flex;
  justify-content: center;
  align-items: center;
  animation: fadeInUp 0.8s var(--ease-out);
}

.hero-text {
  flex: 1;
  max-width: 500px;
  animation: fadeInUp 0.8s var(--ease-out) 0.15s both;
}

.hero-badge {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 5px 14px;
  background: var(--color-bg-card);
  border: 1px solid var(--color-border);
  border-radius: 20px;
  font-size: 12px;
  font-weight: 500;
  color: var(--color-text-secondary);
  letter-spacing: 0.04em;
  margin-bottom: 32px;
}

.badge-dot {
  width: 6px;
  height: 6px;
  border-radius: 50%;
  background: var(--color-primary);
  box-shadow: 0 0 8px var(--color-primary);
}

.hero-title {
  margin-bottom: 24px;
}

.title-line {
  display: block;
  font-family: var(--font-display);
  font-size: clamp(48px, 5vw, 72px);
  font-weight: 800;
  line-height: 1;
  letter-spacing: -0.03em;
  background: linear-gradient(135deg, var(--color-primary) 0%, var(--color-accent) 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.title-sub {
  display: block;
  font-family: var(--font-display);
  font-size: 15px;
  font-weight: 400;
  color: var(--color-text-secondary);
  margin-top: 12px;
  letter-spacing: 3px;
  text-transform: uppercase;
}

.hero-desc {
  font-size: 15px;
  color: var(--color-primary);
  margin-bottom: 16px;
  line-height: 1.7;
  text-wrap: pretty;
  opacity: 0.85;
}

.hero-credits {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 16px;
  padding: 6px 12px;
  background: var(--color-bg-card);
  border: 1px solid var(--color-border);
  border-radius: 6px;
  width: fit-content;
}

.credits-label {
  font-size: 11px;
  color: var(--color-text-secondary);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.credits-link {
  font-size: 13px;
  font-weight: 500;
  color: var(--color-text-secondary);
}

.credits-link:hover {
  opacity: 0.8;
}

.hero-cta {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 10px 22px;
  background: var(--color-primary-dim);
  border: 1px solid var(--color-border-hover);
  border-radius: var(--radius-sm);
  color: var(--color-text);
  font-size: 13px;
  font-weight: 500;
  transition: all 0.3s var(--ease-out);
}

.hero-cta:hover {
  background: rgba(74, 222, 128, 0.15);
  border-color: var(--color-primary);
  color: var(--color-primary);
  opacity: 1;
}

.hero-cta.primary {
  background: var(--color-primary);
  border-color: var(--color-primary);
  color: #0c1210;
  font-weight: 600;
}

.hero-cta.primary:hover {
  background: #6ee7a0;
  border-color: #6ee7a0;
  color: #0c1210;
}

.hero-actions {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

@media (max-width: 1024px) {
  .hero-content {
    flex-direction: column;
    text-align: center;
    gap: 32px;
  }

  .hero-text {
    max-width: 100%;
  }

  .hero-badge,
  .hero-credits {
    margin-left: auto;
    margin-right: auto;
  }

  .hero-actions {
    justify-content: center;
  }
}

@media (max-width: 768px) {
  .hero {
    padding: 32px 0;
  }

  .title-line {
    font-size: 44px;
  }

  .title-sub {
    font-size: 13px;
    letter-spacing: 2px;
  }

  .hero-desc {
    font-size: 14px;
  }

  .hero-cta {
    padding: 9px 18px;
    font-size: 12px;
  }
}
</style>
