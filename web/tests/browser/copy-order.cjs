// Stubbed APIs only. Verifies full prefill, edits, reload/mode switch and POST as a new order.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const base = process.env.TEST_BASE_URL || 'http://127.0.0.1:4178';
const source = {
  shipper: { company: 'Original sender', originalShipper: 'FWD customer', contact: 'Sender contact', tel: '0909000000', address: 'Original pickup address', taxId: 'S-TAX', email: 'sender@example.test', country: 'Vietnam', branch: 'Hà Nội' },
  receiver: { company: 'Original receiver', contact: 'Receiver contact', tel: '91234567', phoneCode: '+65', country: 'Singapore', countryCode: 'SG', city: 'Singapore', state: 'SG', postal: '123456', addr1: 'First address', addr2: 'Second address', addr3: 'Third address', taxId: 'R-TAX', email: 'receiver@example.test', iossNo: '', eoriNo: '' },
  service: { carrier: 'DHL', hub: 'DHL - Original custom hub', reference: 'KEEP-REF' },
  shipment: { type: 'PACK', pieces: '3', grossWeight: '8' },
  goods: { category: '', description: 'Clothes and shoes', docContent: '', multi: [{ category: 'Clothes', note: 'Two boxes' }] },
  packages: [
    { qty: '2', packaging: 'Special crate', category: 'Clothes', description: 'Clothing detail', length: '20.25', width: '30.5', height: '10', weight: '3' },
    { qty: '1', packaging: 'Thùng carton', category: 'Shoes', description: '', length: '10', width: '10', height: '10', weight: '2' }
  ],
  addons: ['Đóng thuế hộ (DDP)', 'Original extra service'],
  invoice: { currency: 'CAD', exportType: 'CUSTOM-EXPORT', dutyTerms: 'DDP', shippingFee: '12.50', items: [
    { descEn: 'Clothes', descVi: 'Quần áo', manufacturer: 'Maker\nOriginal factory', origin: 'CN', hs: '610910', qty: '2', unit: 'PCS', price: '1.25' },
    { descEn: 'Shoes', descVi: 'Giày', manufacturer: 'Shoe maker', origin: 'VN', hs: '640399', qty: '1', unit: 'PCS', price: '2.50' }
  ] }
};
(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_PATH });
  try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
    await context.addInitScript(() => {
      localStorage.setItem('va.tour.v1.COPY-TEST', '1');
      sessionStorage.setItem('va.createOrder.autosave:COPY-TEST:test', JSON.stringify({ receiver: { company: 'Unrelated unsaved order' } }));
    });
    const page = await context.newPage();
    const errors = [];
    const writes = [];
    let fail = false;
    page.on('pageerror', e => errors.push(e.message));
    await context.route('**/api/**', async route => {
      const request = route.request();
      const url = new URL(request.url());
      let data = [];
      if (url.pathname.endsWith('/auth/session')) data = { customerCode: 'COPY-TEST', userName: 'test', companyName: 'Profile must not replace original sender', contactName: 'Profile contact', accountType: 'customer', permissions: ['shipments.create', 'shipments.view'] };
      else if (url.pathname.endsWith('/copy')) {
        if (fail) return route.fulfill({ status: 404, contentType: 'application/json', body: JSON.stringify({ message: 'Không tìm thấy đơn gốc' }) });
        data = source;
      } else if (url.pathname.endsWith('/addresses/countries')) data = [{ code: 'SG', name: 'Singapore', dialCode: '+65' }];
      else if (url.pathname.endsWith('/rates')) return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ rates: [] }) });
      else if (request.method() !== 'GET' && !url.pathname.endsWith('/usage/events')) {
        writes.push({ path: url.pathname, method: request.method(), body: request.postDataJSON() });
        data = { id: 'new-copy', ...request.postDataJSON() };
      }
      return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data, count: Array.isArray(data) ? data.length : undefined }) });
    });
    await page.goto(`${base}/orders/new?from=90000001`);
    const receiver = page.locator('input[name="receiver.company"]');
    await receiver.waitFor();
    assert.equal(await receiver.inputValue(), source.receiver.company);
    assert.equal(await page.locator('input[name="shipper.company"]').inputValue(), source.shipper.company);
    assert.equal(await page.locator('select[name="service.hub"]').inputValue(), source.service.hub);
    assert.equal(await page.locator('input[name="service.reference"]').inputValue(), 'KEEP-REF');
    await receiver.fill('Edited copied receiver');
    await page.getByRole('radiogroup', { name: 'Chế độ tạo đơn' }).getByText('1 trang', { exact: true }).click();
    await page.waitForURL('**/orders/new/quick?from=90000001');
    assert.equal(await receiver.inputValue(), 'Edited copied receiver');
    assert.equal(await page.locator('input[name="packages.0.length"]').inputValue(), '20.25');
    assert.equal(await page.locator('input[name="packages.0.weight"]').inputValue(), '3');
    assert.equal(await page.locator('select[name="packages.0.packaging"]').inputValue(), 'Special crate');
    assert.equal(await page.locator('select[name="invoice.currency"]').inputValue(), 'CAD');
    assert.equal(await page.locator('select[name="invoice.exportType"]').inputValue(), 'CUSTOM-EXPORT');
    assert.equal(await page.locator('textarea[name="invoice.items.0.manufacturer"]').inputValue(), source.invoice.items[0].manufacturer);
    assert.equal(await page.locator('textarea[name="invoice.items.1.descEn"]').inputValue(), 'Shoes');
    assert(await page.getByText('Original extra service', { exact: true }).isVisible());
    await page.reload();
    await receiver.waitFor();
    assert.equal(await receiver.inputValue(), 'Edited copied receiver');
    assert.equal(await page.locator('input[name="packages.0.length"]').inputValue(), '20.25');
    console.log('PASS full form, original decimals, older select values, edits across mode switch and reload');
    await page.getByRole('button', { name: 'Tạo đơn hàng', exact: true }).click();
    await page.waitForURL('**/drafts');
    assert.equal(writes.length, 1);
    assert.equal(writes[0].method, 'POST');
    assert(writes[0].path.endsWith('/drafts'));
    const created = writes[0].body;
    assert.equal(created.stt, 'ready');
    assert.equal(created.payload.receiver.company, 'Edited copied receiver');
    assert.deepEqual(created.payload.packages, source.packages);
    assert.deepEqual(created.payload.invoice, source.invoice);
    assert.deepEqual(created.payload.addons, source.addons);
    assert.equal(created.payload.service.reference, 'KEEP-REF');
    assert.equal(source.receiver.company, 'Original receiver');
    console.log('PASS creates independent new order with copied packages/invoice/addons; source is unchanged');
    fail = true;
    await page.goto(`${base}/orders/new?from=missing`);
    await page.getByText('Không tìm thấy đơn gốc', { exact: false }).waitFor();
    assert.equal(await page.getByRole('button', { name: 'Tạo đơn hàng', exact: true }).count(), 0);
    fail = false;
    await page.getByRole('button', { name: 'Thử lại', exact: true }).click();
    await receiver.waitFor();
    assert.equal(await receiver.inputValue(), 'Original receiver');
    assert.deepEqual(errors, []);
    console.log('PASS load failure blocks empty form submission and can be retried');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });
