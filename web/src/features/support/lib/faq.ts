/** Câu hỏi thường gặp của trang Hỗ trợ & góp ý: lọc theo nhóm + tìm, gợi ý theo vấn đề khách chọn khi gửi yêu cầu. Hàm thuần — có test. */

export type FaqCategory = 'don' | 'cuoc' | 'gui' | 'tk';

export interface FaqItem {
  cat: FaqCategory;
  q: string;
  a: string;
  /** Link tới trang liên quan (vd /drafts), hiện cuối câu trả lời. */
  link?: { to: string; label: string };
}

/** Bỏ dấu, chữ thường — tìm "cuoc" ra "Cước". */
const fold = (s: string) => s.normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase();

/** Lọc theo nhóm (trống = tất cả) và chữ tìm (không dấu, trong câu hỏi hoặc câu trả lời). */
export function searchFaq(items: readonly FaqItem[], cat: FaqCategory | '', query: string): FaqItem[] {
  const q = fold(query.trim());
  return items.filter(f => (!cat || f.cat === cat) && (!q || fold(`${f.q} ${f.a}`).includes(q)));
}

/** Tối đa `limit` câu cùng nhóm với vấn đề khách chọn — hiện trong form gửi yêu cầu trước khi gửi. */
export function faqForTopic(items: readonly FaqItem[], topicCats: Readonly<Record<string, FaqCategory>>, topic: string, limit = 2): FaqItem[] {
  const cat = topicCats[topic];
  return cat ? items.filter(f => f.cat === cat).slice(0, limit) : [];
}
