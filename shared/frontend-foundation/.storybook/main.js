/** @type {import('@storybook/angular').StorybookConfig} */
const config = {
  stories: ['../src/**/*.stories.ts'],
  addons: ['@storybook/addon-a11y'],
  framework: { name: '@storybook/angular', options: {} },
  docs: { autodocs: 'tag' },
};

export default config;
