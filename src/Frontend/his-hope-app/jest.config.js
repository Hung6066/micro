module.exports = {
  preset: 'jest-preset-angular',
  setupFilesAfterEnv: ['<rootDir>/setup-jest.ts'],
  testPathIgnorePatterns: ['/node_modules/', '/dist/', '/.stryker-tmp/'],
  maxWorkers: '50%',
  cacheDirectory: '<rootDir>/.jest-cache',
  moduleNameMapper: {
    '@core/(.*)': '<rootDir>/src/app/core/$1',
    '@shared/(.*)': '<rootDir>/src/app/shared/$1',
    '@features/(.*)': '<rootDir>/src/app/features/$1',
    '@env/(.*)': '<rootDir>/src/environments/$1',
    '@store/(.*)': '<rootDir>/src/app/store/$1',
    '@testing/(.*)': '<rootDir>/src/app/testing/$1',
  },
};
