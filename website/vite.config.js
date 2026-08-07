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
  build: {
    outDir: 'dist',
    // vue3-easy-data-table ships CSS with an invalid `var(easy-table-body-row-font-color)`
    // (missing the `--` custom-property prefix). Vite 8's default lightningcss minifier
    // parses CSS strictly and fails the build on it, so disable CSS minification.
    cssMinify: false,
  },
})
