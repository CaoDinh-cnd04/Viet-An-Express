// Run with PLAYWRIGHT_MODULE pointing to an installed playwright package.
// Uses an isolated browser context and stubbed APIs; never modifies customer data.
const { chromium } = require(process.env.PLAYWRIGHT_MODULE || 'playwright');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const results = [];
const output = process.env.TEST_OUTPUT_DIR || path.join(process.env.TEMP, 'vietan-ui-tests', 'results');
fs.mkdirSync(output, { recursive: true });
const base = process.env.TEST_BASE_URL || 'http://127.0.0.1:5174';
const key = 'mytracking-draft:TEST-MYTRACKING';
const sleep = ms => new Promise(r => setTimeout(r, ms));
async function until(fn, label) {
  for (let i = 0; i < 60; i++) { if (await fn()) return; await sleep(100); }
  throw new Error(label);
}
async function check(name, fn) {
  try { await fn(); results.push({ name, status: 'passed' }); console.log('PASS', name); }
  catch (error) { results.push({ name, status: 'failed', error: error.message }); console.log('FAIL', name, error.message); }
}
(async () => {
 const browser = await chromium.launch({headless:true, executablePath:process.env.CHROMIUM_PATH, timeout:20000});
 try {
 const context = await browser.newContext({viewport:{width:1600,height:1000}});
 const page = await context.newPage();
 const errors = [];
 page.on('pageerror',error=>errors.push(error.message));
 await context.route(url=>url.pathname.startsWith('/api/'),route=> {
   const path = new URL(route.request().url()).pathname;
   const me = path.endsWith('/me') || path.endsWith('/auth/session');
   // Kịch bản này kiểm chế độ lưu bản thử nghiệm trên trình duyệt (backend chưa có API MyTracking → 404).
   if (path.endsWith('/account/mytracking')) return route.fulfill({status:404,contentType:'application/json',body:'{}'});
   return route.fulfill({status:200,contentType:'application/json',body:JSON.stringify(me ? {data:{customerCode:'TEST-MYTRACKING',companyName:'Test Customer',contactName:'Tester',userName:'test',roles:['customer'],permissions:['account.mytracking']}} : {data:[],count:0})});
 });
 await page.goto(base+'/account/mytracking');
 await page.getByRole('heading',{name:'Thông tin thương hiệu',exact:true}).waitFor();
 // Dismiss the first-login walkthrough just as a customer would.
 await page.getByRole('button',{name:'Bỏ qua',exact:true}).click({timeout:10000});
 const section = name => page.locator('form section').filter({has:page.getByRole('heading',{name,exact:true})});
 const brand = section('Thông tin thương hiệu');
 const ads = section('Hình ảnh quảng cáo');
 const background = section('Hình ảnh nền trang');
 const content = section('Nội dung hiển thị chính');
 const contacts = section('Liên hệ và mạng xã hội');
 const preview = page.locator('section').filter({has:page.getByRole('heading',{name:'Xem trước MyTracking',exact:true})}).last();
 const values = () => ads.locator('input[name$=".title"]').evaluateAll(inputs=>inputs.map(input=>input.value));
 const save = async () => {await page.getByRole('button',{name:'Lưu bản thử nghiệm',exact:true}).click(); await until(()=>page.getByText('Đã lưu bản thử nghiệm trên trình duyệt',{exact:true}).isVisible(),'save feedback');};
 const stored = () => page.evaluate(k=>JSON.parse(localStorage.getItem(k)),key);
 const imageBytes = {};
 for (const mime of ['image/png','image/jpeg','image/webp']) {
   imageBytes[mime] = Buffer.from(await page.evaluate(type=>{const canvas=document.createElement('canvas');canvas.width=80;canvas.height=60;const ctx=canvas.getContext('2d');ctx.fillStyle='#278b45';ctx.fillRect(0,0,80,60);return canvas.toDataURL(type).split(',')[1];},mime),'base64');
 }
 await context.route('https://mytracking-assets.test/**',route=> {
   const url = route.request().url();
   if(url.includes('stall')) return new Promise(resolve=>setTimeout(()=>{route.abort().catch(()=>{}).finally(resolve);},17000));
   if(url.includes('broken')) return route.fulfill({status:404,body:'not an image'});
   if(url.includes('html')) return route.fulfill({contentType:'text/html',body:'<html>not an image</html>'});
   return route.fulfill({contentType:'image/png',body:imageBytes['image/png']});
 });
 const upload = (root,name,mime,buffer=imageBytes[mime]) => root.locator('input[type=file]').setInputFiles({name,mimeType:mime,buffer});
 const addUrl = async (root,url,enter=false) => {
   await root.getByRole('textbox',{name:'Nhập URL ảnh từ internet',exact:true}).fill(url);
   if(enter) await root.getByRole('textbox',{name:'Nhập URL ảnh từ internet',exact:true}).press('Enter');
   else await root.getByRole('button',{name:'Thêm từ URL',exact:true}).click();
 };
 await check('Nhập thương hiệu/nội dung cập nhật preview ngay',async()=>{
   await brand.getByLabel('Tên công ty',{exact:true}).fill('Công ty kiểm thử');
   await brand.getByLabel('Địa chỉ',{exact:true}).fill('123 Đường kiểm thử');
   await brand.getByLabel('Số điện thoại',{exact:true}).fill('0912345678');
   await content.getByLabel('Tiêu đề',{exact:false}).fill('Dịch vụ quốc tế');
   await content.getByLabel('Mô tả',{exact:true}).fill('Mô tả dịch vụ\nDòng thứ hai');
   assert(await preview.getByText('Công ty kiểm thử',{exact:true}).isVisible());
   assert(await preview.getByText('Dịch vụ quốc tế',{exact:true}).isVisible());
   assert.equal(await preview.getByRole('link',{name:'Số điện thoại',exact:true}).getAttribute('href'),'tel:0912345678');
 });
 await check('Tải logo PNG, nén thành WebP',async()=>{
   await upload(brand,'logo.png','image/png');
   await until(async()=> (await brand.locator('img').count())===1,'logo uploaded');
   assert((await brand.locator('img').getAttribute('src')).startsWith('data:image/webp;base64,'));
   assert(await preview.getByRole('img',{name:'Logo',exact:true}).isVisible());
 });
 await check('Thay logo bằng URL và xóa logo',async()=>{
   await brand.getByLabel('URL logo',{exact:true}).fill('https://mytracking-assets.test/logo.png');
   await brand.getByRole('button',{name:'Thêm từ URL',exact:true}).click();
   await until(async()=>await brand.locator('img').getAttribute('src')==='https://mytracking-assets.test/logo.png','URL logo');
   await brand.getByRole('button',{name:'Xóa logo',exact:true}).click();
   assert.equal(await brand.locator('img').count(),0);
   await upload(brand,'logo.jpeg','image/jpeg');
   await until(async()=> (await brand.locator('img').count())===1,'JPEG logo');
 });
 await check('Tải ảnh quảng cáo JPG/PNG/WebP bằng giải mã ảnh thật',async()=>{
   for (const [mime,name] of [['image/jpeg','photo.jpg'],['image/png','photo.png'],['image/webp','photo.webp']]) {
     await upload(ads,name,mime);
     await until(async()=>await ads.getByRole('button',{name:'Xóa ảnh',exact:false}).count()===({'photo.jpg':1,'photo.png':2,'photo.webp':3}[name]),'upload '+name);
   }
   for (const src of await ads.locator('img').evaluateAll(images=>images.map(image=>image.src))) assert(src.startsWith('data:image/webp;base64,'));
 });
 await check('Thêm ảnh URL trực tiếp/Enter, đạt tối đa 5 ảnh',async()=>{
   await addUrl(ads,'https://mytracking-assets.test/banner-a.png',true);
   await until(async()=>await ads.getByRole('button',{name:'Xóa ảnh',exact:false}).count()===4,'four images');
   await addUrl(ads,'https://mytracking-assets.test/banner-b.png');
   await until(async()=>await ads.getByRole('button',{name:'Xóa ảnh',exact:false}).count()===5,'five images');
   assert(await ads.getByRole('button',{name:'Thêm ảnh mới',exact:true}).isDisabled());
   assert(await ads.getByRole('textbox',{name:'Nhập URL ảnh từ internet',exact:true}).isDisabled());
 });
 await check('Tiêu đề/chữ nút/link website và vô hiệu hóa nút khi link trống',async()=>{
   for(let i=0;i<5;i++) await ads.locator(`input[name="images.${i}.title"]`).fill('Banner '+i);
   await ads.locator('input[name="images.0.buttonText"]').fill('Liên hệ ngay');
   await ads.getByLabel('Link website của ảnh 1',{exact:true}).fill('https://example.com/service');
   const link=preview.getByRole('link',{name:'Banner 0 Liên hệ ngay',exact:false});
   assert.equal(await link.getAttribute('href'),'https://example.com/service');
   assert.equal(await link.getAttribute('target'),'_blank');
   await ads.locator('input[name="images.0.linkUrl"]').fill('');
   assert.equal(await preview.getByRole('link',{name:'Banner 0',exact:false}).count(),0);
   assert(await preview.getByRole('button',{name:'Liên hệ ngay',exact:true}).isDisabled());
   await ads.locator('input[name="images.0.linkUrl"]').fill('https://example.com/service');
   await ads.locator('input[name="images.0.buttonText"]').fill('');
   assert.equal(await preview.getByText('Liên hệ ngay',{exact:true}).count(),0);
   assert.equal(await preview.getByRole('link',{name:'Banner 0',exact:false}).getAttribute('href'),'https://example.com/service');
   await ads.locator('input[name="images.0.buttonText"]').fill('Liên hệ ngay');
 });
 await check('Đổi thứ tự bằng Lên/Xuống và kéo thả',async()=>{
   await ads.getByRole('button',{name:'Đưa ảnh xuống',exact:true}).first().click();
   assert.deepEqual(await values(),['Banner 1','Banner 0','Banner 2','Banner 3','Banner 4']);
   await ads.getByRole('button',{name:'Đưa ảnh lên',exact:true}).nth(1).click();
   assert.deepEqual(await values(),['Banner 0','Banner 1','Banner 2','Banner 3','Banner 4']);
   const handles=ads.getByRole('button',{name:'Di chuyển ảnh',exact:false});
   // Keep both handles visible while performing a native drag (rows include link guidance).
   await page.setViewportSize({width:1600,height:1800});
   await ads.scrollIntoViewIfNeeded();
   await handles.first().dragTo(handles.nth(2), { targetPosition: { x: 8, y: 8 } });
   await page.setViewportSize({width:1600,height:1000});
   assert.deepEqual(await values(),['Banner 1','Banner 2','Banner 0','Banner 3','Banner 4']);
   assert.equal(await ads.locator('input[name="images.2.linkUrl"]').inputValue(),'https://example.com/service');
 });
 await check('Xóa ảnh, mở lại thêm ảnh và giữ dữ liệu ảnh khác',async()=>{
   await ads.getByRole('button',{name:'Xóa ảnh 5',exact:true}).click();
   assert.equal(await ads.getByRole('button',{name:'Xóa ảnh',exact:false}).count(),4);
   assert(await ads.getByRole('button',{name:'Thêm ảnh mới',exact:true}).isEnabled());
   assert.deepEqual(await values(),['Banner 1','Banner 2','Banner 0','Banner 3']);
 });
 await check('Chặn file quá 200 KB, GIF/SVG và ảnh hỏng',async()=>{
   for(const [name,mime,buffer,message] of [
     ['large.png','image/png',Buffer.alloc(204801),'Ảnh tối đa 200 KB trước khi nén'],
     ['photo.gif','image/gif',Buffer.from('GIF89a'),'Chỉ nhận ảnh JPG, JPEG, PNG hoặc WebP'],
     ['icon.svg','image/svg+xml',Buffer.from('<svg/>'),'Chỉ nhận ảnh JPG, JPEG, PNG hoặc WebP'],
     ['broken.png','image/png',Buffer.from('not png'),'Không đọc được hình ảnh']]) {
     await upload(ads,name,mime,buffer);
     await page.getByText(message,{exact:true}).first().waitFor();
     assert.equal(await ads.getByRole('button',{name:'Xóa ảnh',exact:false}).count(),4);
     assert(await page.getByRole('button',{name:'Lưu bản thử nghiệm',exact:true}).isEnabled());
   }
 });
 await check('Chặn javascript URL, URL 404 và URL trang HTML',async()=>{
   for(const [url,message] of [
     ['javascript:alert(1)','Link phải bắt đầu bằng http:// hoặc https://'],
     ['https://mytracking-assets.test/broken.png','Không tải được ảnh từ URL. Vui lòng dùng link trực tiếp tới ảnh hoặc tải ảnh lên.'],
     ['https://mytracking-assets.test/html','Không tải được ảnh từ URL. Vui lòng dùng link trực tiếp tới ảnh hoặc tải ảnh lên.']]) {
     await addUrl(ads,url); await page.getByText(message,{exact:true}).first().waitFor();
     await until(()=>page.getByRole('button',{name:'Lưu bản thử nghiệm',exact:true}).isEnabled(),'release editor');
     assert.equal(await ads.getByRole('textbox',{name:'Nhập URL ảnh từ internet',exact:true}).inputValue(),url);
     assert.equal(await ads.getByRole('button',{name:'Xóa ảnh',exact:false}).count(),4);
   }
 });
 await check('URL treo: khóa lúc tải, timeout 15 giây rồi mở lại editor',async()=>{
   await page.getByText('Không tải được ảnh từ URL. Vui lòng dùng link trực tiếp tới ảnh hoặc tải ảnh lên.',{exact:true}).first().waitFor({state:'hidden'});
   await addUrl(ads,'https://mytracking-assets.test/stall.png');
   assert(await page.getByRole('button',{name:'Lưu bản thử nghiệm',exact:true}).isDisabled());
   await page.getByText('Không tải được ảnh từ URL. Vui lòng dùng link trực tiếp tới ảnh hoặc tải ảnh lên.',{exact:true}).first().waitFor({timeout:20000});
   await until(()=>page.getByRole('button',{name:'Lưu bản thử nghiệm',exact:true}).isEnabled(),'timeout releases editor');
 });
 await check('Tải/thay/xóa nền bằng file và URL',async()=>{
   await upload(background,'bg.webp','image/webp');
   await until(async()=>await background.locator('img').count()===1,'background upload');
   await addUrl(background,'https://mytracking-assets.test/background.png');
   await until(async()=>await background.locator('img').getAttribute('src')==='https://mytracking-assets.test/background.png','background URL');
   await background.getByRole('button',{name:'Xóa ảnh nền',exact:true}).click();
   assert.equal(await background.locator('img').count(),0);
   await upload(background,'bg.png','image/png');
   await until(async()=>await background.locator('img').count()===1,'background reupload');
 });
 await check('Ảnh đúng 200 KB và URL HTTP thật từ server được nhận',async()=>{
   await upload(ads,'boundary.png','image/png',Buffer.concat([imageBytes['image/png'],Buffer.alloc(204800-imageBytes['image/png'].length)]));
   await until(async()=>await ads.getByRole('button',{name:'Xóa ảnh',exact:false}).count()===5,'boundary image');
   await ads.getByRole('button',{name:'Xóa ảnh 5',exact:true}).click();
   await addUrl(ads,base.replace('127.0.0.1','localhost')+'/logo.webp');
   await until(async()=>await ads.getByRole('button',{name:'Xóa ảnh',exact:false}).count()===5,'real HTTP image');
   assert.equal(await ads.locator('img').last().getAttribute('src'),base.replace('127.0.0.1','localhost')+'/logo.webp');
   await ads.getByRole('button',{name:'Xóa ảnh 5',exact:true}).click();
 });
 await check('Zalo/WhatsApp số điện thoại, mạng xã hội và ẩn khi trống',async()=>{
   await contacts.getByLabel('Zalo',{exact:true}).fill('0912345678');
   await contacts.getByLabel('WhatsApp',{exact:true}).fill('+12025550123');
   for(const network of ['Facebook','Instagram','X']) await contacts.getByLabel(network,{exact:true}).fill('https://example.com/'+network);
   assert.equal(await preview.getByRole('link',{name:'Zalo',exact:true}).getAttribute('href'),'https://zalo.me/84912345678');
   assert.equal(await preview.getByRole('link',{name:'WhatsApp',exact:true}).getAttribute('href'),'https://wa.me/12025550123');
   await contacts.getByLabel('Facebook',{exact:true}).fill('');
   assert.equal(await preview.getByRole('link',{name:'Facebook',exact:true}).count(),0);
 });
 await check('Validation link/tiêu đề, giữ dữ liệu khi lưu bị chặn',async()=>{
   await contacts.getByLabel('Instagram',{exact:true}).fill('javascript:alert(1)');
   await page.getByRole('button',{name:'Lưu bản thử nghiệm',exact:true}).click();
   assert(await contacts.getByText('Link phải bắt đầu bằng http:// hoặc https://',{exact:true}).isVisible());
   assert.equal(await brand.getByLabel('Tên công ty',{exact:true}).inputValue(),'Công ty kiểm thử');
   await contacts.getByLabel('Instagram',{exact:true}).fill('https://example.com/Instagram');
   await content.getByLabel('Tiêu đề',{exact:false}).fill('');
   await page.getByRole('button',{name:'Lưu bản thử nghiệm',exact:true}).click();
   assert(await content.getByText('Nhập tiêu đề MyTracking',{exact:true}).isVisible());
   await content.getByLabel('Tiêu đề',{exact:false}).fill('Dịch vụ quốc tế');
 });
 await check('Lưu/reload giữ mọi trường, ảnh và thứ tự',async()=>{
   await save(); const before=await stored(); assert.equal(before.images.length,4);
   assert.equal(before.images[2].title,'Banner 0'); assert.equal(before.images[2].buttonText,'Liên hệ ngay');
   assert(before.background.startsWith('data:image/webp;'));
   await page.reload(); await brand.getByLabel('Tên công ty',{exact:true}).waitFor();
   await until(async()=>await brand.getByLabel('Tên công ty',{exact:true}).inputValue()==='Công ty kiểm thử','reload data');
   assert.deepEqual(await values(),['Banner 1','Banner 2','Banner 0','Banner 3']);
   assert.deepEqual(await stored(),before);
 });
 await check('Desktop/mobile preview và màn hình hẹp không tràn ngang',async()=>{
   await preview.locator('label').filter({hasText:'Điện thoại'}).click();
   assert(await preview.getByLabel('Điện thoại',{exact:true}).isChecked());
   await page.screenshot({path:path.join(output,'desktop-mobile-preview.png'),fullPage:true});
   await preview.locator('label').filter({hasText:'Máy tính'}).click();
   assert(await preview.getByLabel('Máy tính',{exact:true}).isChecked());
   await page.screenshot({path:path.join(output,'desktop.png'),fullPage:true});
   await page.setViewportSize({width:390,height:844});
   await page.waitForTimeout(300); // Wait for the shared sidebar's responsive slide-out transition.
   assert(await page.evaluate(()=>document.documentElement.scrollWidth<=window.innerWidth));
   await page.screenshot({path:path.join(output,'mobile.png'),fullPage:true,animations:'disabled'});
   await page.setViewportSize({width:1600,height:1000});
 });
 await check('Xuất bản chưa có API: nút bị khóa',async()=>assert(await page.getByRole('button',{name:'Xuất bản (sắp ra mắt)',exact:true}).isDisabled()));
 await check('Khôi phục mặc định chỉ đổi form; lưu xong mới thay bản đã lưu',async()=>{
   const before=await stored(); await page.getByRole('button',{name:'Khôi phục mặc định',exact:true}).click();
   assert.equal(await brand.getByLabel('Tên công ty',{exact:true}).inputValue(),'');
   assert.deepEqual(await stored(),before);
   assert(await page.getByText('Có thay đổi chưa lưu',{exact:true}).isVisible());
   await save(); assert.equal((await stored()).images.length,0); assert.equal((await stored()).brand.companyName,'');
 });
 await check('Không có lỗi JavaScript trong các thao tác',async()=>assert.deepEqual(errors,[]));
 await context.close();
 const isolatedPage = async (code, init) => {
   const isolated = await browser.newContext({viewport:{width:1440,height:900}});
   await isolated.addInitScript(({code, mode})=>{
     localStorage.setItem('va.tour.v1.'+code,'1');
     if(mode==='old') localStorage.setItem('mytracking-draft:'+code,JSON.stringify({title:'Old config',description:'',background:'',images:[{id:'old',src:'https://example.com/photo.png',link:'https://example.com'}]}));
     if(mode==='broken') localStorage.setItem('mytracking-draft:'+code,'{invalid json');
     if(mode==='quota') {
       const set=Storage.prototype.setItem;
       Storage.prototype.setItem=function(k,v){if(k.startsWith('mytracking-draft:'))throw new DOMException('Quota exceeded','QuotaExceededError');return set.call(this,k,v);};
     }
     if(mode==='blocked') {
       const get=Storage.prototype.getItem,set=Storage.prototype.setItem;
       Storage.prototype.getItem=function(k){if(k.startsWith('mytracking-draft:')||k.startsWith('va.mytracking.'))throw new DOMException('Blocked','SecurityError');return get.call(this,k);};
       Storage.prototype.setItem=function(k,v){if(k.startsWith('mytracking-draft:'))throw new DOMException('Blocked','SecurityError');return set.call(this,k,v);};
     }
   },{code,mode:init});
   await isolated.route(url=>url.pathname.startsWith('/api/'),route=>{
     const path=new URL(route.request().url()).pathname;
     if(path.endsWith('/account/mytracking'))return route.fulfill({status:404,contentType:'application/json',body:'{}'});
     const me=path.endsWith('/me')||path.endsWith('/auth/session');
     return route.fulfill({contentType:'application/json',body:JSON.stringify(me?{data:{customerCode:code,companyName:'Test',roles:['customer'],permissions:['account.mytracking']}}:{data:[],count:0})});
   });
   const p=await isolated.newPage();await p.goto(base+'/account/mytracking');
   await p.getByRole('heading',{name:'Thông tin thương hiệu',exact:true}).waitFor();
   return {isolated,p};
 };
 await check('Đọc cấu hình ảnh cũ không có tiêu đề/chữ nút',async()=>{
   const {isolated,p}=await isolatedPage('OLD','old');
   try {assert.equal(await p.locator('input[name=title]').inputValue(),'Old config');assert.equal(await p.locator('input[name="images.0.linkUrl"]').inputValue(),'https://example.com');}
   finally {await isolated.close();}
 });
 await check('JSON cấu hình hỏng vẫn mở form mặc định',async()=>{
   const {isolated,p}=await isolatedPage('BROKEN','broken');
   try {assert.equal(await p.locator('input[name=title]').inputValue(),'DỊCH VỤ TIÊU BIỂU');}
   finally {await isolated.close();}
 });
 for(const mode of ['quota','blocked']) await check('Bộ nhớ '+mode+': báo lỗi, giữ form và bản trong phiên',async()=>{
   const {isolated,p}=await isolatedPage('STORAGE-'+mode,mode);
   try {
     await p.locator('input[name=title]').fill('Retained config');
     await p.getByRole('button',{name:'Lưu bản thử nghiệm',exact:true}).click();
     await p.getByText('Không đủ dung lượng lưu hoặc bộ nhớ bị chặn. Bản thử nghiệm vẫn được giữ trong phiên này.',{exact:true}).waitFor();
     assert.equal(await p.locator('input[name=title]').inputValue(),'Retained config');
     // Menu trái gom nhóm thu gọn: mở nhóm Hỗ trợ rồi sang trang khác trong cùng phiên.
     await p.getByRole('button',{name:/Hỗ trợ/}).first().click();
     await p.locator('a[href="/notifications"]').click();
     await p.waitForURL('**/notifications');
     await p.getByRole('button',{name:/Tài khoản/}).first().click();
     await p.locator('a[href="/account/mytracking"]').click();
     await p.locator('input[name=title]').waitFor();
     assert.equal(await p.locator('input[name=title]').inputValue(),'Retained config');
     // A reload clears the in-memory fallback; no claim of server persistence.
     await p.reload();await p.locator('input[name=title]').waitFor();
     assert.equal(await p.locator('input[name=title]').inputValue(),'DỊCH VỤ TIÊU BIỂU');
   } finally {await isolated.close();}
 });
 await check('Cấu hình được tách riêng theo khách sau khi đổi phiên',async()=>{
   const {isolated,p}=await isolatedPage('ACCOUNT-A');
   try {
     await p.locator('input[name=title]').fill('Config account A');
     await p.getByRole('button',{name:'Lưu bản thử nghiệm',exact:true}).click();
     await p.getByText('Đã lưu bản thử nghiệm trên trình duyệt',{exact:true}).waitFor();
     await isolated.route(url=>url.pathname==='/api/v1/me'||url.pathname==='/api/v1/auth/session',route=>route.fulfill({contentType:'application/json',body:JSON.stringify({data:{customerCode:'ACCOUNT-B',companyName:'Test B',roles:['customer'],permissions:['account.mytracking']}})}));
     await p.evaluate(()=>localStorage.setItem('va.tour.v1.ACCOUNT-B','1'));
     await p.reload();await p.locator('input[name=title]').waitFor();
     assert.equal(await p.locator('input[name=title]').inputValue(),'DỊCH VỤ TIÊU BIỂU');
     await p.locator('input[name=title]').fill('Config account B');
     await p.getByRole('button',{name:'Lưu bản thử nghiệm',exact:true}).click();
     await p.getByText('Đã lưu bản thử nghiệm trên trình duyệt',{exact:true}).waitFor();
     assert.deepEqual(await p.evaluate(()=>['ACCOUNT-A','ACCOUNT-B'].map(code=>JSON.parse(localStorage.getItem('mytracking-draft:'+code)).title)),['Config account A','Config account B']);
   } finally {await isolated.close();}
 });
 } finally {
   await browser.close();
   fs.writeFileSync(path.join(output,'results.json'),JSON.stringify(results,null,2));
   console.log('RESULTS',results.filter(r=>r.status==='passed').length+'/'+results.length,output);
   if(results.some(r=>r.status==='failed'))process.exitCode=1;
 }
})().catch(e=>{console.error(e);process.exitCode=1});
