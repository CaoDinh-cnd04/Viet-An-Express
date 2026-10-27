import { fitWidth, LIMITS } from './editor';

/**
 * Ảnh chọn từ máy → tệp gửi lên: ảnh rộng hơn 1600px hoặc lớn hơn 2 MB được thu nhỏ bằng canvas (JPEG 85%),
 * ảnh nhỏ giữ nguyên tệp gốc. Chỉ nhận JPG / PNG / WebP (backend kiểm lại chữ ký tệp).
 */
export async function prepareImage(file: File): Promise<{ blob: Blob; name: string }> {
  if (!/^image\/(jpeg|png|webp)$/.test(file.type)) throw new Error('Chỉ nhận ảnh JPG, PNG hoặc WebP');
  const bitmap = await createImageBitmap(file);
  try {
    const size = fitWidth(bitmap.width, bitmap.height, LIMITS.imageWidth);
    if (size.width === bitmap.width && file.size <= LIMITS.imageBytes) return { blob: file, name: file.name };
    const canvas = document.createElement('canvas');
    canvas.width = size.width;
    canvas.height = size.height;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('Trình duyệt không thu nhỏ được ảnh');
    // Nền trắng cho ảnh PNG trong suốt khi chuyển sang JPEG.
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, size.width, size.height);
    ctx.drawImage(bitmap, 0, 0, size.width, size.height);
    const blob = await new Promise<Blob | null>(resolve => canvas.toBlob(resolve, 'image/jpeg', 0.85));
    if (!blob || blob.size > LIMITS.imageBytes) throw new Error('Ảnh lớn hơn 2 MB');
    return { blob, name: file.name.replace(/\.\w+$/, '') + '.jpg' };
  } finally {
    bitmap.close();
  }
}
