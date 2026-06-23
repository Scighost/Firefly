import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { fileURLToPath, URL } from 'node:url'
import { readFileSync, existsSync } from 'node:fs'
import { resolve } from 'node:path'

function frameworkCompatPlugin() {
  return {
    name: 'framework-compat',
    enforce: 'pre' as const,
    transform(code: string, id: string) {
      // Fix CRLF line endings in shader files (GLSL compiler can't handle \r\n)
      if (id.includes('cubismshader_webgl')) {
        code = code.replace(/\r\n/g, '\n')
      }
      // Fix getRenderOrders - Core 5.1.0 has it on drawables, not Model
      if (id.includes('cubismmodel.')) {
        code = code.replace(
          'this._model.getRenderOrders()',
          'this._model.drawables.renderOrders'
        )
      }
      return code
    }
  }
}

function frameworkShadersPlugin() {
  return {
    name: 'framework-shaders',
    configureServer(server: any) {
      server.middlewares.use((req: any, res: any, next: any) => {
        if (req.url?.startsWith('/Framework/Shaders/')) {
          const filePath = resolve(__dirname, req.url.slice(1))
          if (existsSync(filePath)) {
            const content = readFileSync(filePath)
            res.setHeader('Content-Type', 'text/plain')
            res.end(content)
            return
          }
        }
        next()
      })
    },
    generateBundle() {
      const shadersDir = resolve(__dirname, 'Framework/Shaders/WebGL')
      const files = [
        'fragshadersrcalphablend.frag',
        'fragshadersrccolorblend.frag',
        'fragshadersrccopy.frag',
        'fragshadersrcmaskinvertedpremultipliedalpha.frag',
        'fragshadersrcmaskpremultipliedalpha.frag',
        'fragshadersrcpremultipliedalpha.frag',
        'fragshadersrcpremultipliedalphablend.frag',
        'fragshadersrcsetupmask.frag',
        'vertshadersrc.vert',
        'vertshadersrcblend.vert',
        'vertshadersrccopy.vert',
        'vertshadersrcmasked.vert',
        'vertshadersrcsetupmask.vert'
      ]
      for (const file of files) {
        const content = readFileSync(resolve(shadersDir, file))
        this.emitFile({
          type: 'asset',
          fileName: `Framework/Shaders/WebGL/${file}`,
          source: content.toString()
        })
      }
    }
  }
}

export default defineConfig({
  plugins: [vue(), frameworkCompatPlugin(), frameworkShadersPlugin()],
  server: {
    host: true,
    port: 5173
  },
  resolve: {
    extensions: ['.ts', '.js', '.tsx', '.jsx', '.json'],
    alias: {
      '@framework': fileURLToPath(new URL('./Framework/src', import.meta.url))
    }
  },
  build: {
    target: 'es2020',
    assetsDir: 'assets',
    outDir: './dist'
  }
})
