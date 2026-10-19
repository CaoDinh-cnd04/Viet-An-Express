/** Nước nhập khẩu có biểu thuế riêng để tra mã HS (mã quốc gia dài hơn 6 số quốc tế). */
export const HS_COUNTRIES = [
  { code: 'US', label: 'Hoa Kỳ (US)', tariff: 'HTSUS', digits: '10 số' },
  { code: 'GB', label: 'Anh (UK)', tariff: 'UK Tariff', digits: '10 số' },
  { code: 'AU', label: 'Úc (AU)', tariff: 'ABF', digits: '8/10 số' }
] as const;

export type HsCountry = (typeof HS_COUNTRIES)[number]['code'];

/**
 * Hiển thị mã HS dễ đọc: 6 số đầu "0804.50", phần quốc gia nhóm 2 số "0804.50.00.10".
 * Giữ nguyên nếu không phải toàn số (vd đã có dấu chấm sẵn thì chuẩn hóa lại).
 */
export function formatHs(code: string | null | undefined): string {
  const d = (code ?? '').replace(/\D/g, '');
  if (d.length < 4) return (code ?? '').trim();
  const parts = [d.slice(0, 4), d.slice(4, 6), ...(d.slice(6).match(/.{1,2}/g) ?? [])].filter(Boolean);
  return parts.join('.');
}

/** 6 số quốc tế (HS6) của 1 mã bất kỳ, "" nếu chưa đủ 6 số. */
export const hs6Of = (code: string | null | undefined): string => {
  const d = (code ?? '').replace(/\D/g, '');
  return d.length >= 6 ? formatHs(d.slice(0, 6)) : '';
};

/** Đủ trường để gom nhóm — khớp HsLookupItem của api.ts (tránh import vòng). */
export interface HsGroupable {
  hs6: string;
  path?: readonly string[] | null;
}

export interface HsGroup<T extends HsGroupable> {
  hs6: string;
  /** Các cấp cha chung của mọi dòng trong nhóm (đưa lên tiêu đề nhóm). */
  path: string[];
  /** Mỗi dòng kèm phần đường dẫn còn lại riêng của nó. */
  lines: { item: T; rest: string[] }[];
}

/**
 * Gom kết quả tra HS theo HS6 (giữ thứ tự backend trả), tách đường dẫn biểu thuế:
 * phần chung của cả nhóm lên tiêu đề, phần riêng ở từng dòng. Hàm thuần — có test.
 */
export function groupByHs6<T extends HsGroupable>(items: readonly T[]): HsGroup<T>[] {
  const groups = new Map<string, T[]>();
  for (const item of items) {
    const key = formatHs(item.hs6);
    groups.set(key, [...(groups.get(key) ?? []), item]);
  }
  return [...groups].map(([hs6, list]) => {
    const paths = list.map(i => [...(i.path ?? [])]);
    let common = paths[0] ?? [];
    for (const p of paths.slice(1)) {
      let n = 0;
      while (n < common.length && n < p.length && common[n] === p[n]) n++;
      common = common.slice(0, n);
    }
    return { hs6, path: common, lines: list.map((item, i) => ({ item, rest: (paths[i] ?? []).slice(common.length) })) };
  });
}
