/*
 * Trang in bill A4 / nhãn A6 (backend OrderDocumentRenderer, ghi vào tab /print của portal): nút copy ảnh theo trang và theo liên.
 * File tĩnh cùng tên miền portal → chạy được với CSP script-src 'self', không phải thêm mã băm.
 * Cách chụp: chép phần tử + CSS của trang vào SVG <foreignObject> → vẽ lên canvas → PNG (ảnh / mã vạch đều nhúng sẵn data:, không tải ngoài).
 * Trình duyệt cho ghi clipboard (https / localhost) → copy luôn; không cho (http) → hiện ảnh để chuột phải "Sao chép hình ảnh" hoặc tải PNG.
 */
(function () {
  'use strict';
  var SCALE = 2;
  /** Lề trắng quanh ảnh (px) — dán vào Zalo / email không bị sát viền. */
  var PAD = 12;
  var doc = document;

  var style = doc.createElement('style');
  style.textContent =
    '.cp-bar{display:flex;justify-content:flex-end;gap:6px;margin:10px auto -6px;font:12px system-ui,sans-serif}' +
    '.cp-bar button{padding:4px 12px;border:1px solid #1e6b2c;border-radius:6px;background:#fff;color:#1e6b2c;font-weight:600;cursor:pointer}' +
    '.cp-bar button:disabled{opacity:.6;cursor:wait}' +
    '.cp-toast{position:fixed;left:50%;bottom:24px;transform:translateX(-50%);padding:8px 16px;border-radius:6px;background:#1e6b2c;color:#fff;font:13px system-ui,sans-serif;z-index:20}' +
    '.cp-modal{position:fixed;inset:0;display:flex;flex-direction:column;align-items:center;justify-content:center;gap:10px;padding:16px;background:rgba(0,0,0,.6);z-index:30;font:13px system-ui,sans-serif;color:#fff}' +
    '.cp-modal img{max-width:92vw;max-height:78vh;background:#fff;box-shadow:0 2px 12px rgba(0,0,0,.4)}' +
    '.cp-modal .cp-actions{display:flex;gap:8px}' +
    '.cp-modal a,.cp-modal button{padding:6px 14px;border:0;border-radius:6px;background:#fff;color:#1e6b2c;font-weight:600;text-decoration:none;cursor:pointer}' +
    '@media print{.cp-bar,.cp-toast,.cp-modal{display:none!important}}';
  doc.head.appendChild(style);

  /** CSS của trang, đổi "body" thành khung bọc để body.a4 / body.no-sender… vẫn áp vào bản chụp. */
  function pageCss() {
    var css = '';
    doc.querySelectorAll('style').forEach(function (s) { if (s !== style) css += s.textContent + '\n'; });
    return css.replace(/(^|[\s,}])body(?=[\s.{:,#[>])/g, '$1.cp-root');
  }

  function toast(text) {
    var t = doc.createElement('div');
    t.className = 'cp-toast';
    t.textContent = text;
    doc.body.appendChild(t);
    setTimeout(function () { t.remove(); }, 2600);
  }

  function fileName(label) {
    var bills = (doc.title.split(' - ').pop() || 'bill').replace(/[^\w,+-]+/g, '_');
    return 'VietAn-' + bills + '-' + label.replace(/\s+/g, '-') + '.png';
  }

  /** Phần tử → Promise<Blob PNG>. */
  function render(el) {
    var rect = el.getBoundingClientRect();
    var w = Math.ceil(rect.width) + PAD * 2, h = Math.ceil(rect.height) + PAD * 2;
    var clone = el.cloneNode(true);
    clone.style.margin = '0';
    clone.style.boxShadow = 'none';
    var root = doc.createElement('div');
    root.className = 'cp-root ' + doc.body.className;
    root.style.cssText = 'box-sizing:border-box;margin:0;padding:' + PAD + 'px;background:#fff;width:' + w + 'px;height:' + h + 'px;overflow:hidden';
    var css = doc.createElement('style');
    css.textContent = pageCss();
    root.appendChild(css);
    root.appendChild(clone);
    root.setAttribute('xmlns', 'http://www.w3.org/1999/xhtml');
    var xhtml = new XMLSerializer().serializeToString(root);
    var svg = '<svg xmlns="http://www.w3.org/2000/svg" width="' + w * SCALE + '" height="' + h * SCALE + '" viewBox="0 0 ' + w + ' ' + h + '">' +
      '<foreignObject x="0" y="0" width="' + w + '" height="' + h + '">' + xhtml + '</foreignObject></svg>';
    return new Promise(function (resolve, reject) {
      var img = new Image();
      img.onload = function () {
        var canvas = doc.createElement('canvas');
        canvas.width = w * SCALE;
        canvas.height = h * SCALE;
        var ctx = canvas.getContext('2d');
        ctx.fillStyle = '#fff';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
        canvas.toBlob(function (blob) { blob ? resolve(blob) : reject(new Error('toBlob')); }, 'image/png');
      };
      img.onerror = function () { reject(new Error('svg')); };
      img.src = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(svg);
    });
  }

  /** Không ghi được clipboard: hiện ảnh để chuột phải sao chép, kèm nút tải PNG. */
  function showImage(blob, label) {
    var url = URL.createObjectURL(blob);
    var modal = doc.createElement('div');
    modal.className = 'cp-modal';
    modal.innerHTML = '<div>Chuột phải vào ảnh → <b>Sao chép hình ảnh</b> (Copy image), hoặc tải file PNG.</div>' +
      '<img alt=""><div class="cp-actions"><a>Tải PNG</a><button type="button">Đóng</button></div>';
    modal.querySelector('img').src = url;
    var a = modal.querySelector('a');
    a.href = url;
    a.download = fileName(label);
    var close = function () { modal.remove(); URL.revokeObjectURL(url); };
    modal.querySelector('button').addEventListener('click', close);
    modal.addEventListener('click', function (e) { if (e.target === modal) close(); });
    doc.body.appendChild(modal);
  }

  function copy(el, label, button) {
    button.disabled = true;
    var done = function () { button.disabled = false; };
    var png = render(el);
    var canClipboard = window.isSecureContext && navigator.clipboard && navigator.clipboard.write && window.ClipboardItem;
    if (canClipboard) {
      // Truyền Promise vào ClipboardItem để giữ "thao tác của người dùng" trong lúc vẽ ảnh (Chrome / Safari).
      navigator.clipboard.write([new ClipboardItem({ 'image/png': png })])
        .then(function () { toast('Đã copy ảnh ' + label + ' — dán (Ctrl+V) vào Zalo, email, Word…'); })
        .catch(function () { return png.then(function (b) { showImage(b, label); }); })
        .catch(function () { toast('Không tạo được ảnh — hãy dùng nút In → Lưu thành PDF'); })
        .then(done);
    } else {
      png.then(function (b) { showImage(b, label); })
        .catch(function () { toast('Không tạo được ảnh — hãy dùng nút In → Lưu thành PDF'); })
        .then(done);
    }
  }

  function button(text, target, label) {
    var b = doc.createElement('button');
    b.type = 'button';
    b.textContent = text;
    b.addEventListener('click', function () { copy(target, label, b); });
    return b;
  }

  var LIEN = ['Liên 1 (người gửi lưu)', 'Liên 2 (bưu cục gốc)', 'Liên 3 (bưu cục phát)'];
  var slipNo = 0;
  doc.querySelectorAll('section.page').forEach(function (page, i) {
    var bar = doc.createElement('div');
    bar.className = 'cp-bar';
    bar.style.width = page.offsetWidth + 'px';
    var n = i + 1;
    bar.appendChild(button('Copy ảnh trang ' + n, page, 'trang ' + n));
    // Bill A4: mỗi tờ có 1–2 liên (.slip) — copy riêng từng liên.
    if (page.classList.contains('sheet')) {
      page.querySelectorAll(':scope > .slip').forEach(function (slip) {
        var name = LIEN[slipNo++ % 3];
        bar.appendChild(button('Copy ' + name.split(' (')[0].toLowerCase(), slip, name));
      });
    }
    page.parentNode.insertBefore(bar, page);
  });
})();
