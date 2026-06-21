import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { viteSingleFile } from 'vite-plugin-singlefile'

// When WINFORGE_SINGLE=1, produce ONE self-contained HTML in dist-single/
// (all JS + CSS inlined, openable directly via file://).
// Otherwise produce a normal multi-file build in dist/ with relative base.
const single = process.env.WINFORGE_SINGLE === '1'

export default defineConfig({
  base: './',
  plugins: [react(), ...(single ? [viteSingleFile()] : [])],
  build: {
    outDir: single ? 'dist-single' : 'dist',
    emptyOutDir: true,
    ...(single
      ? {
          // Inline everything so the output is a single file.
          assetsInlineLimit: 100000000,
          cssCodeSplit: false,
          rollupOptions: {
            output: {
              inlineDynamicImports: true,
            },
          },
        }
      : {}),
  },
})
