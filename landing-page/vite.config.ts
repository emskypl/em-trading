import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    // Azure Static Web Apps publishes this folder (output_location in the deploy workflow).
    outDir: 'build',
  },
})
