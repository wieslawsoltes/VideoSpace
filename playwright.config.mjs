import { defineConfig } from '@playwright/test';
export default defineConfig({
  testDir: './tests/browser', timeout: 180000, expect: { timeout: 20000 }, workers: 1, retries: 0,
  reporter: [['list'], ['html', { outputFolder: 'artifacts/playwright-report', open: 'never' }], ['junit', { outputFile: 'artifacts/browser-results.xml' }]],
  outputDir: 'artifacts/test-results',
  use: { baseURL: process.env.VIDEOSPACE_URL || 'http://127.0.0.1:4173/VideoSpace/', viewport: { width: 1440, height: 900 }, screenshot: 'only-on-failure', trace: 'retain-on-failure', browserName: 'chromium', launchOptions: { args: ['--enable-unsafe-webgpu', '--enable-unsafe-swiftshader', '--enable-features=Vulkan', '--use-angle=vulkan', '--use-vulkan=swiftshader', '--use-webgpu-adapter=swiftshader', '--disable-vulkan-surface', '--autoplay-policy=no-user-gesture-required'] } }
});
