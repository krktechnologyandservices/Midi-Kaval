module.exports = {
  preset: 'react-native',
  setupFiles: ['./jest.setup.js'],
  testMatch: ['**/__tests__/**/*.test.ts', '**/__tests__/**/*.test.tsx'],
  // The first test in a run pays a one-time Babel-transform warm-up cost for native
  // modules that need transforming (see transformIgnorePatterns below) — comfortably
  // under 1s in practice, but tight against the 5s default when run as part of the
  // full suite rather than in isolation.
  testTimeout: 10000,
  transformIgnorePatterns: [
    // The trailing "/" in each alternative only matches an exact package name (e.g.
    // "react-native/"), NOT prefixed packages like "react-native-image-picker/" — those
    // fell through to the default (untransformed) and broke on their raw ESM `import`
    // syntax. List the specific react-native-* packages that ship untranspiled source
    // and need it rather than a "react-native-.*" wildcard — most react-native-* packages
    // already ship pre-compiled CJS, and transforming them anyway roughly doubles the
    // per-suite warm-up cost for no benefit (verified: it pushed unrelated, previously-fine
    // tests past even a generous timeout).
    'node_modules/(?!(react-native|react-native-image-picker|react-native-vector-icons|react-native-fs|react-native-file-viewer|@react-native|@react-navigation|@midi-kaval)/)',
  ],
};
