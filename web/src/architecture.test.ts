import { describe, expect, it } from 'vitest';

/**
 * Quy tắc kiến trúc web (giống Architecture.Tests của backend) — chặn ngay khi chạy test thay vì đợi review:
 * 1. shared/ không phụ thuộc features/ hay app/.
 * 2. features/ không phụ thuộc app/.
 * 3. Feature dùng feature khác chỉ qua cổng công khai `@/features/<tên>` (index.ts) — không import file bên trong,
 *    không dùng đường dẫn tương đối ra ngoài feature của mình.
 * 4. Không có vòng phụ thuộc giữa các feature (A → B → … → A).
 * app/ (router, layout) được import trang của feature để lazy-load.
 */
const sources = import.meta.glob<string>('/src/**/*.{ts,tsx}', { query: '?raw', import: 'default', eager: true });

const IMPORT = /(?:from\s+|import\s*\(\s*|import\s+)['"]([^'"]+)['"]/g;

/** Đường dẫn import → đường dẫn trong src (vd "features/orders/api"); thư viện ngoài → null. */
function resolve(file: string, spec: string): string | null {
  if (spec.startsWith('@/')) return spec.slice(2);
  if (!spec.startsWith('.')) return null;
  const parts = file.replace(/^\/src\//, '').split('/').slice(0, -1);
  for (const seg of spec.split('/')) {
    if (seg === '..') parts.pop();
    else if (seg !== '.') parts.push(seg);
  }
  return parts.join('/');
}

const featureOf = (path: string) => /^features\/([\w-]+)/.exec(path)?.[1] ?? null;

interface Edge { from: string; target: string; file: string }

const edges: Edge[] = Object.entries(sources).flatMap(([file, source]) =>
  [...source.matchAll(IMPORT)].flatMap(m => {
    const target = resolve(file, m[1]!);
    return target ? [{ from: file.replace(/^\/src\//, ''), target, file }] : [];
  })
);

describe('kiến trúc web', () => {
  it('shared/ không phụ thuộc features/ hay app/', () => {
    const bad = edges.filter(e => e.from.startsWith('shared/') && /^(features|app)\//.test(e.target));
    expect(bad.map(e => `${e.from} → ${e.target}`)).toEqual([]);
  });

  it('features/ không phụ thuộc app/', () => {
    const bad = edges.filter(e => e.from.startsWith('features/') && e.target.startsWith('app/'));
    expect(bad.map(e => `${e.from} → ${e.target}`)).toEqual([]);
  });

  it('feature dùng feature khác chỉ qua @/features/<tên> (index.ts)', () => {
    const bad = edges.filter(e => {
      const a = featureOf(e.from);
      const b = featureOf(e.target);
      return a !== null && b !== null && a !== b && e.target !== `features/${b}`;
    });
    expect(bad.map(e => `${e.from} → ${e.target}`)).toEqual([]);
  });

  it('không có vòng phụ thuộc giữa các feature', () => {
    const graph = new Map<string, Set<string>>();
    for (const e of edges) {
      const a = featureOf(e.from);
      const b = featureOf(e.target);
      if (a && b && a !== b) graph.set(a, (graph.get(a) ?? new Set()).add(b));
    }
    const cycles: string[] = [];
    const visit = (node: string, path: string[]) => {
      for (const next of graph.get(node) ?? []) {
        const i = path.indexOf(next);
        if (i >= 0) cycles.push([...path.slice(i), next].join(' → '));
        else visit(next, [...path, next]);
      }
    };
    for (const start of graph.keys()) visit(start, [start]);
    expect([...new Set(cycles)]).toEqual([]);
  });
});
