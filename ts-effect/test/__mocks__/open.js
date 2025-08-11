// Mock for the 'open' package to avoid ES module issues in tests
module.exports = () => Promise.resolve();