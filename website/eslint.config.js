import js from '@eslint/js'
import pluginVue from 'eslint-plugin-vue'
import prettier from 'eslint-config-prettier'
import globals from 'globals'

export default [
   {
      ignores: ['dist/**', 'node_modules/**'],
   },
   js.configs.recommended,
   ...pluginVue.configs['flat/essential'],
   prettier,
   {
      files: ['**/*.{js,vue}'],
      languageOptions: {
         ecmaVersion: 'latest',
         sourceType: 'module',
         globals: {
            ...globals.browser,
         },
      },
      rules: {
         // App.vue is intentionally a single-word root component
         'vue/multi-word-component-names': 'off',
         'no-unused-vars': ['warn', { argsIgnorePattern: '^_' }],
      },
   },
]
