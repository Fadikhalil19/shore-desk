import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The built app is served by the ASP.NET Core API from its wwwroot folder,
// so the browser talks to one address and sign-in cookies just work.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../Desk.Api/wwwroot',
    emptyOutDir: true,
  },
})
