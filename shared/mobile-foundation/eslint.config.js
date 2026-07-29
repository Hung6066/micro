// @ts-check
import tseslint from 'typescript-eslint';

// Platform-neutral contracts package \u2014 no Angular here.
export default tseslint.config(
  {
    files: ['src/**/*.ts'],
    extends: [...tseslint.configs.recommended],
    rules: {
      'no-eval': 'error',
    },
  },
);
