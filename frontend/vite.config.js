import { defineConfig } from 'vite';
import { resolve } from 'node:path';

export default defineConfig({
  base: './',
  worker: {
    format: 'es'
  },
  build: {
    target: 'es2022',
    outDir: resolve(process.cwd(), '../ViewerDist'),
    emptyOutDir: true,
    sourcemap: false,
    assetsInlineLimit: 0,
    lib: {
      entry: resolve(process.cwd(), 'src/viewer-entry.js'),
      formats: ['es'],
      fileName: () => 'viewer.js',
      cssFileName: 'viewer'
    },
    rollupOptions: {
      output: {
        chunkFileNames: 'chunks/[name]-[hash].js',
        assetFileNames: 'assets/[name]-[hash][extname]'
      }
    }
  }
});
