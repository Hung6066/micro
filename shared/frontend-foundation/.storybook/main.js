/** @type {import('@storybook/angular').StorybookConfig} */
module.exports = {
  stories: ['../src/**/*.stories.ts'],
  addons: ['@storybook/addon-a11y'],
  framework: { name: '@storybook/angular', options: {} },
  docs: { autodocs: 'tag' },
};
