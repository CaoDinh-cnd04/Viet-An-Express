/**
 * Chép chữ vào clipboard. `navigator.clipboard` chỉ có trên https (hoặc localhost);
 * site chạy http thì dùng cách cũ: ô textarea ẩn + execCommand('copy').
 */
export async function copyText(text: string): Promise<void> {
  if (window.isSecureContext && navigator.clipboard) {
    await navigator.clipboard.writeText(text);
    return;
  }
  const area = document.createElement('textarea');
  area.value = text;
  area.setAttribute('readonly', '');
  area.style.position = 'fixed';
  area.style.opacity = '0';
  document.body.appendChild(area);
  const focused = document.activeElement as HTMLElement | null;
  area.select();
  try {
    if (!document.execCommand('copy')) throw new Error('Trình duyệt không cho phép sao chép');
  } finally {
    area.remove();
    focused?.focus();
  }
}
