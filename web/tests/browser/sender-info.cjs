// All API calls are stubbed. No live database changes or emails are sent.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const base = process.env.TEST_BASE_URL || 'http://127.0.0.1:4178';
const fields = ['companyName', 'contactName', 'phone', 'email', 'address', 'taxCode'];
let profile = { companyName: 'Công ty ban đầu', contactName: 'Nguyễn A', phone: '0901000000', email: 'ops@example.test', address: '12 Địa chỉ cũ, TP.HCM', taxCode: '0312345678' };
const requests = [];
const errors = [];
let nextId = 1;
let staffReads = 0;
const changed = r => r.changed.some(k => (r.old[k] || '') !== (profile[k] || ''));
const adminRow = r => ({ request: r, customerCode: 'SENDER-TEST', companyName: profile.companyName, userName: 'main', live: profile, outdated: r.status === 'pending' && changed(r), reviewedBy: r.status === 'pending' ? null : 'CS Việt An' });

(async () => {
  const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_PATH });
  try {
    async function pageFor(role) {
      const context = await browser.newContext({ viewport: { width: 1440, height: 1000 } });
      await context.addInitScript(role => {
        localStorage.setItem('va.tour.v1.SENDER-TEST', '1');
        if (role === 'admin') sessionStorage.setItem('va.admin.token', 'sender-admin');
      }, role);
      const page = await context.newPage();
      page.on('pageerror', e => errors.push(e.message));
      await context.route('**/api/**', async route => {
        const req = route.request();
        const url = new URL(req.url());
        const path = url.pathname.replace('/api/v1', '');
        let data = [];
        const fail = (status, message) => route.fulfill({ status, contentType: 'application/json', body: JSON.stringify({ message }) });
        if (path === '/auth/session') data = role === 'admin' ? null : { ...profile, customerCode: 'SENDER-TEST', userName: role, accountType: role === 'staff' ? 'staff' : 'customer', fullName: profile.contactName, permissions: ['shipments.view', 'shipments.create', ...(role === 'staff' ? [] : ['account.sender-info'])] };
        else if (path === '/account/sender-info') {
          if (role === 'staff') { staffReads++; return fail(403, 'Không có quyền'); }
          data = { current: profile, pending: requests.find(r => r.status === 'pending') || null, history: [...requests].reverse().filter(r => r.status !== 'pending') };
        } else if (path === '/account/sender-info/requests') {
          if (requests.some(r => r.status === 'pending')) return fail(409, 'Đang có yêu cầu chờ duyệt');
          const body = req.postDataJSON();
          const proposed = Object.fromEntries(fields.map(k => [k, (body[k] || '').trim() || null]));
          data = { id: nextId++, status: 'pending', old: { ...profile }, new: proposed, changed: fields.filter(k => (profile[k] || '') !== (proposed[k] || '')), note: body.note, reason: null, createdAt: '13/10/2026 17:00', reviewedAt: null };
          requests.push(data);
        } else if (/\/account\/sender-info\/requests\/\d+\/cancel$/.test(path)) {
          const id = Number(path.split('/').at(-2));
          const r = requests.find(r => r.id === id);
          r.status = 'cancelled'; r.reviewedAt = '13/10/2026 17:01'; data = r;
        } else if (path === '/admin/sender-info') {
          assert.equal(req.headers().authorization, 'Bearer sender-admin');
          const status = url.searchParams.get('status');
          data = requests.filter(r => !status || r.status === status).map(adminRow);
        } else if (/\/admin\/sender-info\/\d+\/(approve|reject)$/.test(path)) {
          assert.equal(req.headers().authorization, 'Bearer sender-admin');
          const id = Number(path.split('/').at(-2));
          const r = requests.find(r => r.id === id);
          if (path.endsWith('/approve')) {
            if (changed(r)) return fail(409, 'Hồ sơ đã thay đổi trong lúc chờ duyệt');
            for (const k of r.changed) profile[k] = r.new[k];
            r.status = 'approved';
          } else { r.status = 'rejected'; r.reason = req.postDataJSON().reason; }
          r.reviewedAt = '13/10/2026 17:02'; data = adminRow(r);
        }
        return route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify({ data, count: Array.isArray(data) ? data.length : undefined }) });
      });
      return page;
    }
    const customer = await pageFor('main');
    const admin = await pageFor('admin');
    await customer.goto(`${base}/account/sender-info`);
    await customer.getByRole('heading', { name: 'Đề nghị thay đổi thông tin', exact: true }).waitFor();
    assert(await customer.getByRole('button', { name: 'Gửi yêu cầu xét duyệt' }).isDisabled());
    await customer.getByLabel('Điện thoại').fill('0902000000');
    await customer.getByLabel('Lý do / ghi chú thay đổi').fill('Đổi số điện thoại liên hệ');
    await customer.getByRole('button', { name: 'Gửi yêu cầu xét duyệt' }).click();
    await customer.getByRole('heading', { name: 'Yêu cầu đang chờ duyệt', exact: true }).waitFor();
    assert.equal(profile.phone, '0901000000');
    assert.equal(requests[0].new.phone, '0902000000');
    await customer.setViewportSize({ width: 390, height: 844 });
    assert(await customer.evaluate(() => document.documentElement.scrollWidth <= innerWidth), 'customer sender page must fit mobile viewport');
    await customer.setViewportSize({ width: 1440, height: 1000 });

    await admin.goto(`${base}/admin`);
    await admin.getByRole('tab', { name: /Duyệt người gửi/ }).click();
    await admin.getByRole('button', { name: /Công ty ban đầu/ }).click();
    const dialog = admin.getByRole('dialog');
    await dialog.waitFor();
    assert(await dialog.getByRole('button', { name: 'Từ chối yêu cầu', exact: true }).isDisabled());
    assert(await dialog.getByText('0901000000', { exact: true }).count() >= 1);
    assert(await dialog.getByText('0902000000', { exact: true }).isVisible());
    await dialog.getByLabel('Lý do từ chối', { exact: true }).fill('Cần xác minh số điện thoại mới');
    await dialog.getByRole('button', { name: 'Từ chối yêu cầu', exact: true }).click();
    await dialog.waitFor({ state: 'hidden' });
    assert.equal(profile.phone, '0901000000');
    await customer.getByRole('button', { name: 'Làm mới', exact: true }).focus();
    await customer.getByRole('button', { name: 'Làm mới', exact: true }).press('Enter');
    await customer.getByRole('heading', { name: 'Đề nghị thay đổi thông tin', exact: true }).waitFor();
    await customer.locator('details summary').first().click();
    await customer.getByText('Cần xác minh số điện thoại mới', { exact: false }).waitFor();
    console.log('PASS submission does not change profile; admin comparison, required rejection reason, customer history and mobile view');

    async function submitPhone(phone) {
      await customer.getByLabel('Điện thoại').fill(phone);
      await customer.getByRole('button', { name: 'Gửi yêu cầu xét duyệt' }).click();
      await customer.getByRole('heading', { name: 'Yêu cầu đang chờ duyệt', exact: true }).waitFor();
    }
    await submitPhone('0903000000');
    await customer.getByRole('button', { name: 'Hủy yêu cầu', exact: true }).click();
    await customer.getByRole('heading', { name: 'Đề nghị thay đổi thông tin', exact: true }).waitFor();
    assert.equal(requests[1].status, 'cancelled');
    assert.equal(profile.phone, '0901000000');
    await submitPhone('0904000000');
    await admin.getByRole('button', { name: 'Làm mới', exact: true }).click();
    await admin.getByRole('button', { name: /Công ty ban đầu/ }).click();
    await dialog.waitFor();
    await dialog.getByRole('button', { name: 'Duyệt & cập nhật hồ sơ', exact: true }).click();
    await dialog.waitFor({ state: 'hidden' });
    assert.equal(profile.phone, '0904000000');
    await customer.getByRole('button', { name: 'Làm mới', exact: true }).focus();
    await customer.getByRole('button', { name: 'Làm mới', exact: true }).press('Enter');
    await customer.getByRole('heading', { name: 'Đề nghị thay đổi thông tin', exact: true }).waitFor();
    assert.equal(await customer.getByLabel('Điện thoại').inputValue(), '0904000000');
    await customer.goto(`${base}/orders/new/quick`);
    await customer.locator('input[name="shipper.tel"]').waitFor({ state: 'attached' });
    assert.equal(await customer.locator('input[name="shipper.tel"]').inputValue(), '0904000000');
    console.log('PASS cancel, approve, updated customer profile and approved sender details prefilled on new orders');

    await customer.goto(`${base}/account/sender-info`);
    await submitPhone('0905000000');
    profile.phone = '0909999999';
    await admin.getByRole('button', { name: 'Làm mới', exact: true }).click();
    await admin.getByRole('button', { name: /Công ty ban đầu/ }).click();
    await dialog.waitFor();
    assert(await dialog.getByRole('button', { name: 'Duyệt & cập nhật hồ sơ', exact: true }).isDisabled());
    await dialog.getByText('Hồ sơ đã thay đổi trong lúc chờ duyệt.', { exact: false }).waitFor();
    const staff = await pageFor('staff');
    await staff.goto(`${base}/account/sender-info`);
    await staff.getByText('Bạn không có quyền truy cập trang này', { exact: true }).waitFor();
    assert.equal(staffReads, 0);
    assert.equal(await staff.locator('a[href="/account/sender-info"]').count(), 0);
    assert.deepEqual(errors, []);
    console.log('PASS stale profile cannot be approved; staff cannot access sender change page or menu');
  } finally { await browser.close(); }
})().catch(e => { console.error(e); process.exitCode = 1; });


