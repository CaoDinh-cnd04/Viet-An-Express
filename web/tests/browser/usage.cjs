// Stubbed API responses only; never writes to a real customer database.
// PLAYWRIGHT_MODULE may point to playwright or playwright-core; CHROMIUM_PATH is optional.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const base = process.env.TEST_BASE_URL || 'http://127.0.0.1:4178';

(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_PATH });
  try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
    await context.addInitScript(() => { sessionStorage.setItem('va.admin.token', 'test-admin'); localStorage.setItem('va.tour.v1.ANALYTICS-TEST', '1'); });
    const page = await context.newPage();
    const errors = [];
    const events = [];
    const requests = [];
    let customer = false;
    let empty = false;
    page.on('pageerror', e => errors.push(e.message));
    await context.route('**/api/**', async route => {
      const url = new URL(route.request().url());
      let data = [];
      if (url.pathname.endsWith('/auth/session')) data = customer ? { customerCode: 'ANALYTICS-TEST', userName: 'test', companyName: 'Test', permissions: ['shipments.create', 'shipments.view'] } : null;
      else if (url.pathname.endsWith('/usage/events')) { events.push(route.request().postDataJSON()); data = true; }
      else if (url.pathname.endsWith('/admin/analytics')) {
        assert.equal(route.request().headers().authorization, 'Bearer test-admin');
        requests.push(url.searchParams.get('audience'));
        data = empty ? { views: 0, visitors: 0, pages: [], modes: [{ mode: 'wizard', views: 0, visitors: 0, starts: 0, completed: 0, switches: 0 }, { mode: 'quick', views: 0, visitors: 0, starts: 0, completed: 0, switches: 0 }], days: [] }
          : { views: 100, visitors: 20, pages: [{ path: '/orders/new', views: 70, visitors: 15 }, { path: '/orders/new/quick', views: 30, visitors: 8 }], modes: [{ mode: 'wizard', views: 70, visitors: 15, starts: 10, completed: 6, switches: 2 }, { mode: 'quick', views: 30, visitors: 8, starts: 8, completed: 7, switches: 3 }], days: [{ date: '2026-10-13', views: 100, visitors: 20 }] };
      }
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data, count: Array.isArray(data) ? data.length : undefined }) });
    });
    await page.goto(`${base}/admin`);
    await page.getByRole('tab', { name: 'Thống kê sử dụng' }).click();
    await page.getByRole('heading', { name: 'So sánh hai cách tạo đơn' }).waitFor();
    assert.equal(await page.getByText('60.0%', { exact: true }).count(), 1);
    assert.equal(events.length, 0, 'admin page must not be tracked');
    await page.getByLabel('Loại tài khoản').selectOption('staff');
    await page.waitForResponse(r => r.url().includes('audience=staff'));
    assert(requests.includes('staff'));
    await page.setViewportSize({ width: 390, height: 844 });
    assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'mobile page must not overflow');
    empty = true;
    await page.getByRole('button', { name: 'Làm mới', exact: true }).click();
    await page.getByText('Chưa có dữ liệu truy cập', { exact: true }).waitFor();
    console.log('PASS admin report, filtering, empty state, mobile layout');

    customer = true;
    await page.setViewportSize({ width: 1440, height: 1000 });
    await page.goto(`${base}/orders/new`);
    await page.locator('input[name="shipper.contact"]').waitFor();
    const skip = page.getByRole('button', { name: 'Bỏ qua', exact: true });
    if (await skip.isVisible()) await skip.click();
    const field = page.locator('input[name="shipper.contact"]');
    await field.fill('Browser test');
    await field.fill('Browser test updated');
    await page.waitForTimeout(300);
    assert.equal(events.filter(e => e.kind === 'order_start').length, 1);
    const modes = page.getByRole('radiogroup', { name: 'Chế độ tạo đơn' });
    await modes.getByText('1 trang', { exact: true }).click();
    await page.waitForURL('**/orders/new/quick');
    await page.waitForTimeout(300);
    assert.equal(events.filter(e => e.kind === 'mode_switch').length, 1);
    await page.locator('input[name="shipper.contact"]').fill('Quick test');
    await page.waitForTimeout(300);
    assert.equal(events.filter(e => e.kind === 'order_start').length, 2);
    assert.equal(new Set(events.filter(e => e.flowId).map(e => e.flowId)).size, 1);
    await page.reload();
    await page.locator('input[name="shipper.contact"]').fill('Reload test');
    await page.waitForTimeout(300);
    assert.equal(events.filter(e => e.kind === 'order_start').length, 2);
    assert.deepEqual(errors, []);
    console.log('PASS route views, form starts, mode switches, reload deduplication');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
