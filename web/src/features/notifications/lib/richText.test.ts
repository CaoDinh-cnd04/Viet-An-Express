import { describe, expect, it } from 'vitest';
import { editorValue, isRichText, plainToHtml, sanitizeHtml, sizeLabel, stepFontSize, toPlainText } from './richText';

describe('sanitizeHtml — khớp RichText.Sanitize ở backend', () => {
  it('giữ định dạng cho phép, bỏ thuộc tính và thẻ nguy hiểm', () => {
    expect(
      sanitizeHtml(
        '<p onclick="x()">Phụ phí <b>tăng</b> <span style="color: rgb(220, 38, 38); font-size: 18px; position: fixed" class="a">2%</span><script>alert(1)</script></p>' +
          '<a href="javascript:alert(1)">bấm</a><img src=x onerror=alert(1)><u>gạch</u><i>nghiêng</i>'
      )
    ).toBe('<p>Phụ phí <b>tăng</b> <span style="color: rgb(220, 38, 38); font-size: 18px">2%</span></p>bấm<u>gạch</u><i>nghiêng</i>');
  });

  it.each([
    ['<span style="color: red; background: url(x)">a</span>', '<span>a</span>'],
    ['<span style="font-size: 200px">a</span>', '<span>a</span>'],
    ['<span style="font-weight: 700; text-decoration-line: underline">a</span>', '<span style="font-weight: bold; text-decoration: underline">a</span>'],
    ['<b>chưa đóng <i>thẻ', '<b>chưa đóng <i>thẻ</i></b>'],
    ['a < b &amp; c & d<br/>e', 'a &lt; b &amp; c &amp; d<br>e'],
    ['<style>p{}</style><!-- x --><div>ok</div></span>', '<div>ok</div>']
  ])('%s', (input, expected) => expect(sanitizeHtml(input)).toBe(expected));
});

describe('sanitizeHtml media — HTML thông báo popup của CRM', () => {
  it('giữ liên kết, ảnh, tiêu đề an toàn; chế độ thường vẫn bỏ', () => {
    const html =
      '<h2>Lịch nghỉ lễ</h2><p>Xem <a href="https://vietanexpress.com/tin" onclick="x()">tại đây</a></p>' +
      '<img src="/UserFile/editor/images/a.png" width="300px" height="abc" onerror="alert(1)" alt="Lịch"><hr>';
    expect(sanitizeHtml(html, { media: true })).toBe(
      '<h2>Lịch nghỉ lễ</h2><p>Xem <a href="https://vietanexpress.com/tin" target="_blank" rel="noopener noreferrer">tại đây</a></p>' +
        '<img src="/UserFile/editor/images/a.png" alt="Lịch" width="300"><hr>'
    );
    expect(sanitizeHtml(html)).toBe('Lịch nghỉ lễ<p>Xem tại đây</p>');
  });

  it.each([
    ['<a href="javascript:alert(1)">a</a>', '<a>a</a>'],
    ['<a href="java\tscript:alert(1)">a</a>', '<a>a</a>'],
    ['<a href="//evil.com">a</a>', '<a>a</a>'],
    ['<a href=mailto:cs@vietan.vn>a</a>', '<a href="mailto:cs@vietan.vn" target="_blank" rel="noopener noreferrer">a</a>'],
    ['<img src="data:image/png;base64,AAA">x', 'x'],
    ['<img src="javascript:alert(1)"/>x', 'x'],
    ['<img src=\'https://a.vn/x.png?a=1&amp;b="2"\'/>', '<img src="https://a.vn/x.png?a=1&amp;b=&quot;2&quot;">']
  ])('%s', (input, expected) => expect(sanitizeHtml(input, { media: true })).toBe(expected));
});

describe('isRichText / toPlainText / plainToHtml / editorValue', () => {
  it('nhận diện chữ thường và lấy chữ thuần', () => {
    expect(isRichText('Giá < 2 kg\nvẫn là chữ thường')).toBe(false);
    expect(isRichText('<p>a</p>')).toBe(true);
    expect(toPlainText('<p>Dòng <b>1</b></p><p>Dòng 2 &amp; 3</p>')).toBe('Dòng 1\nDòng 2 & 3');
    expect(toPlainText(' a < b ')).toBe('a < b');
  });

  it('chữ thường mở trong trình soạn giữ xuống dòng, ký tự đặc biệt', () => {
    expect(plainToHtml('a < b\nc')).toBe('a &lt; b<br>c');
    expect(plainToHtml('<p>x</p>')).toBe('<p>x</p>');
  });

  it('giá trị lưu: chỉ có chữ → chữ thường, có định dạng → HTML lọc, trống → rỗng', () => {
    expect(editorValue('a &lt; b')).toBe('a < b');
    expect(editorValue('Tăng <b>2%</b><script>x</script>')).toBe('Tăng <b>2%</b>');
    expect(editorValue('<p><br></p>')).toBe('');
    expect(editorValue('<div> &nbsp;</div>')).toBe('');
  });
});

describe('cỡ chữ trên thanh công cụ', () => {
  it('tăng / giảm 1 bậc, dừng ở đầu và cuối danh sách', () => {
    expect(stepFontSize(14.5, 1)).toBe(16);
    expect(stepFontSize(15, 1)).toBe(16);
    expect(stepFontSize(15, -1)).toBe(14);
    expect(stepFontSize(17, -1)).toBe(16);
    expect(stepFontSize(32, 1)).toBe(32);
    expect(stepFontSize(12, -1)).toBe(12);
  });

  it('hiện cỡ làm tròn, giá trị lạ thì để trống', () => {
    expect(sizeLabel(14.5)).toBe('15');
    expect(sizeLabel(NaN)).toBe('');
  });
});
