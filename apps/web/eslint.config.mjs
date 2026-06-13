import nx from '@nx/eslint-plugin'
import eslintConfigPrettier from 'eslint-config-prettier/flat'
import baseConfig from '../../eslint.config.mjs'

export default [
  ...nx.configs['flat/react'],
  ...baseConfig,
  {
    files: ['**/*.ts', '**/*.tsx', '**/*.js', '**/*.jsx'],
    // Override or add rules here
    rules: {},
  },
  // Disables ESLint rules that conflict with Prettier. Must be last so it wins.
  eslintConfigPrettier,
]
