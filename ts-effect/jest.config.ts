/** @type {import('ts-jest').JestConfigWithTsJest} */
export default {
  preset: 'ts-jest',
  testEnvironment: 'node',
  moduleNameMapper: {
    '^@src/(.*)$': '<rootDir>/src/$1',
    '^@test/(.*)$': '<rootDir>/test/$1'
  },
  testMatch: ['**/*.test.ts'],
  transform: {
    // '^.+\.tsx?$': 'ts-jest', // default for ts-jest preset
  },
  // Automatically clear mock calls, instances, contexts and results before every test
  clearMocks: true
}
