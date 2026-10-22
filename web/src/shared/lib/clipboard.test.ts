import { afterEach, describe, expect, it, vi } from 'vitest';
import { copyText } from './clipboard';

/** Môi trường test là node: dựng document tối thiểu cho nhánh textarea + execCommand. */
function fakeDocument(copyResult: boolean) {
  const area = { value: '', style: {}, setAttribute: vi.fn(), select: vi.fn(), remove: vi.fn() };
  let copied = '';
  const doc = {
    activeElement: null,
    body: { appendChild: vi.fn() },
    createElement: vi.fn(() => area),
    execCommand: vi.fn(() => {
      copied = area.value;
      return copyResult;
    })
  };
  vi.stubGlobal('document', doc);
  return { area, copied: () => copied };
}

describe('copyText', () => {
  afterEach(() => vi.unstubAllGlobals());

  it('https: dùng navigator.clipboard', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    vi.stubGlobal('window', { isSecureContext: true });
    vi.stubGlobal('navigator', { clipboard: { writeText } });
    await copyText('VAHCM123');
    expect(writeText).toHaveBeenCalledWith('VAHCM123');
  });

  it('http: chép qua textarea ẩn rồi gỡ textarea', async () => {
    vi.stubGlobal('window', { isSecureContext: false });
    vi.stubGlobal('navigator', {});
    const { area, copied } = fakeDocument(true);
    await copyText('VAHCM123');
    expect(copied()).toBe('VAHCM123');
    expect(area.remove).toHaveBeenCalled();
  });

  it('http: trình duyệt từ chối thì báo lỗi và vẫn gỡ textarea', async () => {
    vi.stubGlobal('window', { isSecureContext: false });
    vi.stubGlobal('navigator', {});
    const { area } = fakeDocument(false);
    await expect(copyText('x')).rejects.toThrow();
    expect(area.remove).toHaveBeenCalled();
  });
});
