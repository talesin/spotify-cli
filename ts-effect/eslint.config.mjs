import tseslint from 'typescript-eslint'
import { fileURLToPath } from 'node:url'
import path from 'node:path'
import js from '@eslint/js'
import { FlatCompat } from '@eslint/eslintrc'
import typescriptEslint from '@typescript-eslint/eslint-plugin'
import prettier from 'eslint-plugin-prettier'

const __filename = fileURLToPath(import.meta.url)
const __dirname = path.dirname(__filename)
const compat = new FlatCompat({
  baseDirectory: __dirname,
  recommendedConfig: js.configs.recommended,
  allConfig: js.configs.all
})

/** @type {import('eslint').Linter.Config[]} */
export default tseslint.config({
  ignores: [
      '**/node_modules',
      '**/package.lock.json',
      '**/dist',
      '**/build',
      'eslint.config.mjs',
      'jest.config.ts',
      '**/coverage',
      '**/*.js'
    ]
  },
  { files: ['**/*.ts'] },

  ...compat.extends(
    'eslint:recommended',
    'plugin:@typescript-eslint/eslint-recommended',
    'plugin:@typescript-eslint/recommended',
    'plugin:prettier/recommended'
  ),
  {
    plugins: {
      '@typescript-eslint': typescriptEslint,
      prettier
    },

    languageOptions: {
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname
      }
    }
  },

  {
    rules: {
      '@typescript-eslint/no-var-requires': 'error',
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/no-non-null-assertion': 'error',
      '@typescript-eslint/no-unused-vars': [
        'error',
        { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }
      ],
      '@typescript-eslint/no-require-imports': 'error',
      '@typescript-eslint/strict-boolean-expressions': 'error'
    }
  }
)
