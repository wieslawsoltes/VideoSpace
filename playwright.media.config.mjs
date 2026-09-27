import { defineConfig } from '@playwright/test';
export default defineConfig({ testDir: './tests/media', timeout: 120000, workers: 1, reporter: 'list', use: { baseURL: 'http://127.0.0.1:4174', browserName: 'chromium', launchOptions: { args: ['--enable-unsafe-webgpu', '--enable-unsafe-swiftshader', '--use-angle=swiftshader', '--autoplay-policy=no-user-gesture-required'] } } });
