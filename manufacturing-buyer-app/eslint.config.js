// @ts-check
import tseslint from "typescript-eslint";
import angular from "angular-eslint";

export default tseslint.config(
  { ignores: ["dist/**", "out-tsc/**"] },
  {
    files: ["**/*.ts"],
    extends: [...tseslint.configs.recommended, ...angular.configs.tsRecommended],
    processor: angular.processInlineTemplates,
    rules: {
      "@typescript-eslint/no-unused-vars": "warn",
      "@angular-eslint/prefer-inject": "warn",
      "@angular-eslint/no-output-native": "warn",
      "@angular-eslint/no-empty-lifecycle-method": "warn",
    },
  },
  {
    files: ["**/*.html"],
    extends: [...angular.configs.templateRecommended, ...angular.configs.templateAccessibility],
    rules: {
      "@angular-eslint/template/click-events-have-key-events": "warn",
      "@angular-eslint/template/interactive-supports-focus": "warn",
      "@angular-eslint/template/label-has-associated-control": "warn",
    },
  },
);
