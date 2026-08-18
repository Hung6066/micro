// @ts-check
import tseslint from 'typescript-eslint';
import angular from 'angular-eslint';

export default tseslint.config(
  { ignores: ['dist/**', 'storybook-static/**', 'out-tsc/**'] },
  {
    files: ['**/*.ts'],
    extends: [
      ...tseslint.configs.recommended,
      ...angular.configs.tsRecommended,
    ],
    processor: angular.processInlineTemplates,
    rules: {
      // Shared components are consumed under the `hh-` prefix across every app.
      '@angular-eslint/directive-selector': ['error', { type: 'attribute', prefix: 'hh', style: 'camelCase' }],
      '@angular-eslint/component-selector': ['error', { type: 'element', prefix: 'hh', style: 'kebab-case' }],
      'no-eval': 'error',
      '@typescript-eslint/no-unused-vars': 'warn',
      '@typescript-eslint/no-unused-expressions': 'warn',
      '@angular-eslint/prefer-inject': 'warn',
      '@angular-eslint/component-selector': 'warn',
      '@angular-eslint/no-output-native': 'warn',
      '@angular-eslint/no-empty-lifecycle-method': 'warn',
      '@angular-eslint/prefer-standalone': 'warn',
      '@typescript-eslint/no-this-alias': 'warn',
    },
  },
  {
    files: ['**/*.html'],
    extends: [
      ...angular.configs.templateRecommended,
      ...angular.configs.templateAccessibility,
    ],
    rules: {
      '@angular-eslint/template/click-events-have-key-events': 'warn',
      '@angular-eslint/template/interactive-supports-focus': 'warn',
      '@angular-eslint/template/no-autofocus': 'warn',
      '@angular-eslint/template/role-has-required-aria': 'warn',
      '@angular-eslint/template/label-has-associated-control': 'warn',
      '@angular-eslint/template/eqeqeq': 'warn',
    },
  },
);
