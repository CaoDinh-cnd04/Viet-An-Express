/** Bỏ dấu tiếng Việt (giữ chữ hoa / thường): "Đức Anh" → "Duc Anh". Dùng cho tìm kiếm không dấu, đường dẫn, mã. */
export const stripDiacritics = (s: string): string =>
  s
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D');

/** Có chữ tiếng Việt có dấu không ("xoài sấy" → true, "Dried mango" → false) — ô phải ghi tiếng Anh. */
export const hasDiacritics = (s: string): boolean => stripDiacritics(s.normalize('NFC')) !== s.normalize('NFC');
