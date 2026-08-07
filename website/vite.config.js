import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    port: 8080,
  },
  css: {
    lightningcss: {
      // vue3-easy-data-table ships CSS with an invalid `var(easy-table-body-row-font-color)`
      // (missing the `--` custom-property prefix). Vite 8's default lightningcss minifier
      // parses CSS strictly and fails the whole build on it, so enable errorRecovery to
      // skip the invalid rule instead of aborting minification.
      // TODO: remove once https://github.com/HC200ok/vue3-easy-data-table fixes the CSS.
      errorRecovery: true,
    },
  },
  build: {
    outDir: 'dist',
  },
})
