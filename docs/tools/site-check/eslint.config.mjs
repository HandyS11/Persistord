/*
 * Mirrors the SonarQube JavaScript analysis locally so a code smell is caught
 * by `npm run lint` instead of on the server after the merge.
 */
import js from '@eslint/js'
import sonarjs from 'eslint-plugin-sonarjs'
import globals from 'globals'

export default [
  { ignores: ['out/', 'node_modules/'] },
  js.configs.recommended,
  sonarjs.configs.recommended,
  {
    languageOptions: {
      ecmaVersion: 'latest',
      sourceType: 'module',
      /* Browser globals: callbacks handed to page.evaluate / page.$eval run in the page. */
      globals: { ...globals.node, ...globals.browser },
    },
    rules: {
      /* Sonar rules that eslint-plugin-sonarjs delegates to ESLint core (see its
         README, "Improved ESLint rules"): S9382, promises awaited sequentially
         in a loop. */
      'no-await-in-loop': 'error',
    },
  },
  {
    files: ['tests/**'],
    rules: {
      /* A browser test drives one page step by step: each await depends on the
         state the previous one left behind, which is the rule's own exception. */
      'no-await-in-loop': 'off',
      /* A test body is one linear scenario; splitting it hides what is asserted. */
      'sonarjs/cognitive-complexity': 'off',
      /* The waits cover CSS transitions and docfx's deferred tab restore, which
         expose no observable completion state to synchronise on. */
      'sonarjs/no-fixed-wait-in-tests': 'off',
    },
  },
]
