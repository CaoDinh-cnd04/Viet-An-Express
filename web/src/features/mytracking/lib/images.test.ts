import { afterEach, describe, expect, it, vi } from 'vitest';
import { COMPRESSED_IMAGE_BYTES } from '../schema';
import { normalizeImageUrl, readImage, validateImageUrl } from './images';

describe('normalizeImageUrl', () => {
  it('lấy link ảnh thật từ Google Hình ảnh / link chuyển hướng Google', () => {
    const img = 'https://cdn.example.com/a.jpg';
    expect(normalizeImageUrl(`https://www.google.com/imgres?imgurl=${encodeURIComponent(img)}&imgrefurl=x`)).toBe(img);
    expect(normalizeImageUrl(`https://www.google.com.vn/url?url=${encodeURIComponent(img)}&sa=t`)).toBe(img);
  });
  it('link chia sẻ Google Drive → ảnh thu nhỏ của Drive', () => {
    expect(normalizeImageUrl('https://drive.google.com/file/d/1AbC-d_E/view?usp=sharing')).toBe('https://drive.google.com/thumbnail?id=1AbC-d_E&sz=w1600');
    expect(normalizeImageUrl('https://drive.google.com/open?id=XYZ')).toBe('https://drive.google.com/thumbnail?id=XYZ&sz=w1600');
  });
  it('link khác giữ nguyên (bỏ khoảng trắng)', () => {
    expect(normalizeImageUrl(' https://cdn.example.com/a.png ')).toBe('https://cdn.example.com/a.png');
    expect(normalizeImageUrl('không phải link')).toBe('không phải link');
  });
});

afterEach(() => { vi.unstubAllGlobals(); vi.useRealTimers(); });

describe('remote image validation', () => {
  it('rejects unsafe protocols before loading an image', async () => {
    await expect(validateImageUrl('javascript:alert(1)')).rejects.toThrow('https://');
  });
  function mockImage() {
    const image = { naturalWidth: 100, onload: null as (() => void) | null, onerror: null as (() => void) | null, src: '', referrerPolicy: '' };
    vi.stubGlobal('Image', class { constructor() { return image; } });
    return image;
  }

  it('accepts a loaded image without a CORS fetch', async () => {
    const image = mockImage();
    const result = validateImageUrl('  https://example.com/image  ');
    expect(image.src).toBe('https://example.com/image');
    expect(image.referrerPolicy).toBe('no-referrer');
    image.onload?.();
    await expect(result).resolves.toBe('https://example.com/image');
    expect(image.onload).toBeNull();
  });

  it('rejects a broken image URL', async () => {
    const image = mockImage();
    const result = validateImageUrl('https://example.com/page');
    const assertion = expect(result).rejects.toThrow('Sao chép địa chỉ hình ảnh');
    image.onerror?.();
    await assertion;
    expect(image.src).toBe('');
  });

  it('rejects an image with no decoded pixels', async () => {
    const image = mockImage();
    image.naturalWidth = 0;
    const assertion = expect(validateImageUrl('https://example.com/empty')).rejects.toThrow('không phải link file ảnh');
    image.onload?.();
    await assertion;
  });

  it('times out so a stalled URL cannot keep the editor busy', async () => {
    vi.useFakeTimers();
    mockImage();
    const result = validateImageUrl('https://example.com/stalled');
    const assertion = expect(result).rejects.toThrow('không phải link file ảnh');
    await vi.advanceTimersByTimeAsync(15000);
    await assertion;
  });
});

function mockCanvas(outputSizes: number[]) {
  const bitmap = { width: 2000, height: 1000, close: vi.fn() };
  const drawImage = vi.fn();
  let calls = 0;
  const canvas = {
    width: 0, height: 0,
    getContext: () => ({ drawImage }),
    toBlob: (callback: (blob: Blob | null) => void) => {
      const size = outputSizes[Math.min(calls++, outputSizes.length - 1)] ?? 0;
      callback(new Blob([new Uint8Array(size)], { type: 'image/webp' }));
    }
  };
  vi.stubGlobal('createImageBitmap', vi.fn().mockResolvedValue(bitmap));
  vi.stubGlobal('document', { createElement: () => canvas });
  vi.stubGlobal('FileReader', class {
    result: string | null = null;
    onload: (() => void) | null = null;
    onerror: (() => void) | null = null;
    readAsDataURL() { this.result = 'data:image/webp;base64,AA=='; this.onload?.(); }
  });
  return { canvas, bitmap, drawImage };
}

describe('image compression', () => {
  it('reduces dimensions when lowering quality is insufficient and releases the bitmap', async () => {
    const { canvas, bitmap, drawImage } = mockCanvas([
      COMPRESSED_IMAGE_BYTES + 1, COMPRESSED_IMAGE_BYTES + 1,
      COMPRESSED_IMAGE_BYTES + 1, COMPRESSED_IMAGE_BYTES
    ]);
    const result = await readImage(new File(['image'], 'photo.jpg', { type: 'image/jpeg' }));
    expect(result).toBe('data:image/webp;base64,AA==');
    expect(canvas.width).toBe(1200);
    expect(canvas.height).toBe(600);
    expect(drawImage).toHaveBeenCalledTimes(2);
    expect(bitmap.close).toHaveBeenCalledOnce();
  });

  it('rejects images that remain too large instead of storing them', async () => {
    const { bitmap } = mockCanvas([COMPRESSED_IMAGE_BYTES + 1]);
    await expect(readImage(new File(['image'], 'photo.png', { type: 'image/png' })))
      .rejects.toThrow('200 KB');
    expect(bitmap.close).toHaveBeenCalledOnce();
  });
});
