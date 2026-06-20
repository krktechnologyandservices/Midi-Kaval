const path = require('path');

const monorepoRoot = path.resolve(__dirname, '../..');

module.exports = {
  project: {
    android: {
      sourceDir: './android',
      appName: 'app',
    },
  },
  reactNativePath: path.resolve(monorepoRoot, 'node_modules/react-native'),
};
