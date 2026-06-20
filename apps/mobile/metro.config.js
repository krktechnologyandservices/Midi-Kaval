const {getDefaultConfig, mergeConfig} = require('@react-native/metro-config');
const path = require('path');

const projectRoot = __dirname;
const monorepoRoot = path.resolve(projectRoot, '../..');

const config = {
  watchFolders: [monorepoRoot],
  resolver: {
    nodeModulesPaths: [
      path.resolve(projectRoot, 'node_modules'),
      path.resolve(monorepoRoot, 'node_modules'),
    ],
    extraNodeModules: {
      '@midi-kaval/shared-types': path.resolve(
        monorepoRoot,
        'packages/shared-types',
      ),
      '@midi-kaval/api-client': path.resolve(
        monorepoRoot,
        'packages/api-client',
      ),
    },
  },
};

module.exports = mergeConfig(getDefaultConfig(projectRoot), config);
