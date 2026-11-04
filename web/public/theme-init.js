// Khách mới: giao diện sáng (không theo chế độ tối của máy); đã chọn thì dùng lựa chọn đã lưu. Chạy trước React để không nháy tối.
// Tách khỏi index.html để CSP không cần cho phép script inline (script-src 'self').
try { document.documentElement.dataset.theme = JSON.parse(localStorage.getItem('va.theme')) === 'dark' ? 'dark' : 'light'; }
catch (e) { document.documentElement.dataset.theme = 'light'; }
