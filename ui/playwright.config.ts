import { defineConfig, devices } from '@playwright/test';

/**
 * End-to-end tests run against the Angular dev server in the `e2e` configuration: sample mock
 * data, no persistence, no backend. Port 4300 keeps them away from a normal `npm start` on 4200.
 */
export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: !!process.env['CI'],
  retries: process.env['CI'] ? 2 : 0,
  // More local workers than this overload the dev server on Windows (blank pages, ERR_ADDRESS_IN_USE).
  workers: process.env['CI'] ? undefined : 3,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    // IPv4 loopback: IPv6 (::1) connections are blocked on this machine.
    baseURL: 'http://127.0.0.1:4300',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
  },
  projects: [
    {
      name: 'desktop',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } },
    },
    { name: 'mobile', use: { ...devices['Pixel 7'] } },
  ],
  webServer: {
    command: 'npm start -- --configuration e2e --port 4300',
    url: 'http://127.0.0.1:4300',
    reuseExistingServer: !process.env['CI'],
    timeout: 180_000,
  },
});
