// @ts-check
const tseslint = require('typescript-eslint');

// Platform-neutral contracts package \u2014 no Angular here.
module.exports = tseslint.config(
  {
    files: ['**/*.ts'],
    extends: [...tseslint.configs.recommended],
    rules: {
      '@typescript-eslint/no-implied-eval': 'error',
      'no-eval': 'error',
    },
  },
);
