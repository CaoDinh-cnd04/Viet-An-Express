/** Mô tả sự cố ghép từ các câu gợi ý: mỗi câu kết thúc bằng dấu chấm, cách nhau 1 khoảng trắng. */

const sentence = (s: string) => `${s.trim().replace(/[.!?]+$/, '')}.`;

/** Mô tả đã có câu gợi ý này chưa. */
export const hasSentence = (desc: string, s: string): boolean => desc.includes(sentence(s));

/** Bấm câu gợi ý: chưa có thì thêm vào cuối, có rồi thì bỏ ra (khách vẫn sửa tay được phần còn lại). */
export function toggleSentence(desc: string, s: string): string {
  const item = sentence(s);
  if (desc.includes(item)) return desc.replace(item, '').replace(/\s{2,}/g, ' ').trim();
  const base = desc.trim();
  if (!base) return item;
  return `${/[.!?]$/.test(base) ? base : `${base}.`} ${item}`;
}
