// All API calls are stubbed; this check never saves orders or writes customer data.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const base = process.env.TEST_BASE_URL || 'http://127.0.0.1:4178';
(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_PATH });
  try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
    await context.addInitScript(() => localStorage.setItem('va.tour.v1.WEIGHT-TEST', '1'));
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    await context.route('**/api/**', async route => {
      const url = new URL(route.request().url());
      const data = url.pathname.endsWith('/auth/session') ? { customerCode: 'WEIGHT-TEST', userName: 'test', companyName: 'Test', permissions: ['shipments.create', 'shipments.view'] } : [];
      await route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data, count: Array.isArray(data) ? data.length : undefined }) });
    });
    await page.goto(`${base}/orders/new/quick`);
    const card = page.locator('section').filter({ has: page.getByRole('heading', { name: 'Chi tiết kiện hàng', exact: true }) });
    const metric = label => card.locator('dl > div').filter({ has: page.locator('dt').filter({ hasText: new RegExp(`^${label}$`) }) }).locator('dd');
    const fill = (i, field, value) => page.locator(`input[name="packages.${i}.${field}"]`).fill(value);
    await card.waitFor();
    await card.getByRole('button', { name: 'Thêm kiện', exact: true }).click();
    for (const [i, dims, kg] of [[0, ['20', '30', '40'], '6'], [1, ['30', '50', '10'], '2']]) {
      for (const [j, field] of ['length', 'width', 'height'].entries()) await fill(i, field, dims[j]);
      await fill(i, 'weight', kg);
    }
    assert.equal(await metric('Cân thực').textContent(), '8 kg');
    assert.equal(await metric('Quy đổi').textContent(), '7.8 kg');
    assert.equal(await metric('Tính cước').textContent(), '9 kg');
    assert.equal(await card.getByRole('columnheader', { name: 'Tính cước (kg)', exact: true }).count(), 1);
    assert(await card.getByText('Quy đổi = D×R×C ÷ 5000', { exact: true }).isVisible());
    console.log('PASS screenshot example: actual 8, volume 7.8, billable 9; divisor 5000');
    await card.getByRole('button', { name: 'Xóa dòng kiện 2', exact: true }).click();
    await fill(0, 'qty', '3');
    await fill(0, 'length', '1'); await fill(0, 'width', '1'); await fill(0, 'height', '1'); await fill(0, 'weight', '1.1');
    const carrier = page.locator('select[name="service.carrier"]');
    const hub = page.locator('select[name="service.hub"]');
    await carrier.selectOption('UPS'); await hub.selectOption('UPS - VN Saver');
    assert.equal(await metric('Tính cước').textContent(), '4.5 kg');
    await hub.selectOption('UPS - VN Expedited');
    assert.equal(await metric('Tính cước').textContent(), '6 kg');
    await carrier.selectOption('DHL'); await hub.selectOption('DHL - VN');
    assert.equal(await metric('Tính cước').textContent(), '4.5 kg');
    await carrier.selectOption('Fedex'); await hub.selectOption('Fedex - VN');
    assert.equal(await metric('Tính cước').textContent(), '3.5 kg');
    await fill(0, 'qty', '1'); await fill(0, 'weight', '31.1');
    assert.equal(await metric('Tính cước').textContent(), '32 kg');
    await hub.selectOption('Fedex - SIN 3 (FICP)');
    assert.equal(await metric('Tính cước').textContent(), '31.5 kg');
    assert(await card.getByText('Quy đổi = D×R×C ÷ 5000', { exact: true }).isVisible());
    await page.setViewportSize({ width: 390, height: 844 });
    await page.waitForTimeout(300);
    const bounds = await card.boundingBox();
    assert(bounds.x >= 0 && bounds.x + bounds.width <= 390, 'package card must fit mobile viewport');
    assert(await card.locator('table').evaluate(table => { const container = table.parentElement; return getComputedStyle(container).overflowX === 'auto' && container.scrollWidth > container.clientWidth; }), 'package table must scroll within its card on mobile');
    assert.deepEqual(errors, []);
    console.log('PASS live carrier/hub changes, per-piece quantity rounding, FedEx FICP threshold, mobile layout');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
