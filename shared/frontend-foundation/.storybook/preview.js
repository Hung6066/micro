/** @type {import('@storybook/angular').Preview} */
module.exports = {
  parameters: {
    layout: 'padded',
    a11y: { test: 'error' },
    backgrounds: { default: 'workspace', values: [{ name: 'workspace', value: '#F6F8F6' }, { name: 'dark', value: '#111815' }] },
  },
  globalTypes: {
    theme: { description: 'Shared theme', defaultValue: 'light', toolbar: { title: 'Theme', icon: 'paintbrush', items: ['light', 'dark'] } },
  },
};
