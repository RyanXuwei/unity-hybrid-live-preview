// Hybrid Live Preview, final WebGL build (package-owned; refreshed from the Unity package, do not edit).
// Used by Tools > Hybrid Live Preview > Build WebGL and Preview WebGL Build: your vite.config.js plus the
// plugin that merges the Unity player into the output. Your own config stays untouched.
import { defineConfig, mergeConfig } from 'vite';
import userConfig from '../../vite.config.js';
import { hybridWebGLBuild } from './webgl-build-plugin.js';

export default defineConfig(async (env) => {
  const user = typeof userConfig === 'function' ? await userConfig(env) : userConfig;
  return mergeConfig(user ?? {}, { plugins: [hybridWebGLBuild()] });
});
