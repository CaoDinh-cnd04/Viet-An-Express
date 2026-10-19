// Browser checks use isolated contexts and intercepted APIs. No customer data is written.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const base = process.env.TEST_BASE_URL || 'http://127.0.0.1:4178';
const main = { customerCode: 'INTEGRITY-AUDIT', userName: 'main', companyName: 'Audit company', contactName: 'Audit contact', phone: '0901234567', accountType: 'customer', isAdmin: true,
  permissions: ['shipments.view', 'shipments.create', 'shipments.update', 'shipments.issue-bill', 'account.staff', 'account.mytracking', 'account.password', 'account.troubles', 'account.notifications', 'ecommerce.view', 'ecommerce.orders', 'ecommerce.settings', 'ecommerce.connect'] };
const ticket = { id: 'TRB-12', kind: 'su_co', bill: 'VAHN6000001SG', cnee: 'Receiver', ct: 'Singapore', type: 'Giao chậm / trễ hẹn', subject: null, lv: 'low', desc: 'Audit incident description', req: 'Audit contact', contact: '0901234567', date: '15/10/2026 10:00', updatedAt: '15/10/2026 10:00', status: 'new' };
const staff = [
  { id: 1, userName: 'nguyena', fullName: 'Nguyễn Văn An', email: 'an@example.test', phone: '0901000000', permissions: ['shipments.view'], active: true, createdAt: '2026-10-15T10:00:00' },
  { id: 2, userName: 'tranb', fullName: 'Trần Bình', email: null, phone: null, permissions: [], active: false, createdAt: '2026-10-15T10:00:00' }
];
const writes = [];
let role = main;
async function mock(route) {
  const req = route.request(), p = new URL(req.url()).pathname.replace('/api/v1', '');
  let data = [];
  if (p === '/auth/session') data = role;
  else if (p === '/orders') return route.fulfill({ json: { items: [], total: 0, page: 1, pageSize: 20, totalPages: 0, summary: { statusCounts: { all: 0, wait: 0, fly: 0, nd: 0, ok: 0 }, totalPieces: 0, totalWeight: 0 } } });
  else if (p === '/notifications') return route.fulfill({ json: { data: [], unreadCount: 0 } });
  else if (p === '/account/mytracking') return route.fulfill({ status: 404, json: { message: 'Test local fallback' } });
  else if (p === '/account/staff/permissions') data = [{ code: 'shipments.view', description: 'Xem đơn hàng' }, { code: 'shipments.create', description: 'Tạo đơn hàng' }];
  else if (p === '/account/staff') data = staff;
  else if (p === '/troubles') data = [ticket];
  else if (p === '/admin/troubles') data = [{ ticket, customerCode: 'INTEGRITY-AUDIT', companyName: 'Audit company', isStaff: false }];
  else if (p === '/ecom/settings') data = { apiKeys: [], webhookUrl: '', webhookEvents: [] };
  else if (p.endsWith('/countries')) data = [{ code: 'SG', name: 'Singapore', dialCode: '+65' }];
  if (req.method() !== 'GET') {
    writes.push({ path: p, body: req.postData() });
    if (p === '/troubles') data = { ...ticket, id: 'TRB-13' };
    if (p === '/account/staff') { const body = req.postDataJSON(); data = { ...body, id: 3, active: true, createdAt: '2026-10-15T10:00:00' }; staff.push(data); }
  }
  await route.fulfill({ json: { data, count: Array.isArray(data) ? data.length : undefined, message: 'Đã lưu kiểm thử' } });
}
(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_PATH });
  try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
    await context.addInitScript(() => { localStorage.setItem('va.tour.v1.INTEGRITY-AUDIT', '1'); sessionStorage.setItem('va.admin.token', 'audit-token'); });
    await context.route('**/api/**', mock);
    const page = await context.newPage();
    page.setDefaultTimeout(10000);
    const errors = [];
    page.on('pageerror', e => errors.push(e.message));
    const paths = ['/', '/tracking', '/orders', '/orders/new', '/orders/new/quick', '/orders/import', '/drafts', '/pricing', '/ecommerce', '/ecommerce/orders', '/notifications', '/account/api-tracking', '/account/mytracking', '/account/staff', '/account/password', '/troubles', '/admin'];
    for (const path of paths) {
      await page.goto(base + path);
      try { await page.locator('main,h1').first().waitFor(); }
      catch (error) { console.error('Route failed', path, await page.locator('body').innerText(), errors); throw error; }
      await page.waitForLoadState('networkidle');
      assert.equal(await page.getByText('Không tìm thấy trang', { exact: true }).count(), 0, path);
      assert.deepEqual(errors, [], path);
      console.log('PASS route renders', path);
    }
    for (const path of ['/help', '/account/feedback']) {
      await page.goto(base + path); await page.waitForURL('**/troubles');
      await page.getByRole('heading', { name: 'Hỗ trợ & sự cố', exact: true }).waitFor();
      console.log('PASS legacy redirect', path);
    }
    await page.goto(base + '/account/sender-info');
    await page.waitForURL('**/troubles?new=sender-info');
    let dialog = page.getByRole('dialog');
    await dialog.getByLabel('Tiêu đề', { exact: true }).fill('Thay địa chỉ người gửi');
    await dialog.getByLabel('Nội dung', { exact: true }).fill('Đề nghị cập nhật địa chỉ nhận hàng của công ty.');
    await dialog.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
    await dialog.getByRole('button', { name: 'Gửi yêu cầu', exact: true }).click();
    await dialog.waitFor({ state: 'hidden' });
    const support = writes.find(w => w.path === '/troubles');
    assert(support.body.includes('ho_tro')); assert(support.body.includes('Sửa thông tin người gửi'));
    assert(!writes.some(w => w.path.includes('sender-info')));
    console.log('PASS sender profile change becomes support request');

    await page.getByRole('button', { name: /^Góp ý/ }).click();
    dialog = page.getByRole('dialog');
    await dialog.getByLabel('Nội dung', { exact: true }).fill('Giao diện thuận tiện, mong bổ sung hướng dẫn in nhãn.');
    await dialog.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
    await dialog.getByLabel('SĐT / email liên hệ').fill('');
    await dialog.getByRole('button', { name: 'Gửi yêu cầu', exact: true }).click();
    await dialog.waitFor({ state: 'hidden' });
    assert(writes.filter(w => w.path === '/troubles').at(-1).body.includes('gop_y'));
    console.log('PASS feedback without contact or bill');

    await page.goto(base + '/troubles?bill=VAHN6000001SG');
    dialog = page.getByRole('dialog');
    await dialog.getByRole('button', { name: 'Giao chậm / trễ hẹn', exact: true }).click();
    await dialog.getByLabel('Nội dung', { exact: true }).fill('Tracking chưa được cập nhật sau nhiều ngày.');
    await dialog.getByRole('button', { name: 'Tiếp tục', exact: true }).click();
    await dialog.getByRole('button', { name: 'Gấp', exact: true }).click();
    await dialog.getByRole('button', { name: 'Gửi yêu cầu', exact: true }).click();
    await dialog.waitFor({ state: 'hidden' });
    const incident = writes.filter(w => w.path === '/troubles').at(-1).body;
    assert(incident.includes('su_co') && incident.includes('VAHN6000001SG'));
    console.log('PASS incident from selected order');

    await page.goto(base + '/account/staff');
    await page.getByText('Nguyễn Văn An', { exact: true }).waitFor();
    await page.getByRole('textbox', { name: 'Tìm nhân viên' }).fill('nguyen van an');
    assert.equal(await page.getByText('Trần Bình', { exact: true }).count(), 0);
    await page.getByRole('textbox', { name: 'Tìm nhân viên' }).fill('');
    await page.getByRole('button', { name: 'Đã khóa', exact: true }).click();
    assert.equal(await page.getByText('Nguyễn Văn An', { exact: true }).count(), 0);
    await page.getByRole('button', { name: 'Thêm nhân viên', exact: true }).click();
    dialog = page.getByRole('dialog');
    await dialog.getByLabel('Tên đăng nhập', { exact: false }).fill('new.audit');
    await dialog.getByLabel(/^Mật khẩu/).fill('AuditTest123');
    await dialog.getByLabel('Nhập lại mật khẩu', { exact: false }).fill('AuditTest123');
    await dialog.getByLabel('Họ tên', { exact: false }).fill('Nhân viên kiểm thử');
    await dialog.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click();
    await dialog.waitFor({ state: 'hidden' });
    assert(writes.some(w => w.path === '/account/staff' && JSON.parse(w.body).userName === 'new.audit'));
    console.log('PASS staff search without accents, locked filter, create');
    await page.setViewportSize({ width: 390, height: 844 });
    for (const path of ['/account/staff', '/troubles']) {
      await page.goto(base + path); await page.waitForLoadState('networkidle');
      assert(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1), path);
    }
    assert.deepEqual(errors, []);
    console.log('PASS mobile staff and support, no uncaught JavaScript errors');
  } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode = 1; });
