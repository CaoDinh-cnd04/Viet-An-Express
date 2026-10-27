import { useState } from 'react';
import { useSession } from '@/features/auth';
import { customerTrackingUrl, MYTRACKING_URL } from '@/features/mytracking';
import { TRACKING_URL, trackingLink } from '@/shared/config/domain';
import { useI18n } from '@/shared/i18n';
import { useCopyToClipboard } from '@/shared/lib/useCopyToClipboard';
import { Button, Card, Icon, LinkButton, Notice, PageHeader, TextField } from '@/shared/ui';
import styles from './account.module.css';

/** Mã HTML ô tra cứu để khách dán vào web — chữ trên ô theo ngôn ngữ đang chọn. */
const embedCode = (brand: boolean, placeholder: string, submit: string) =>
  [
    `<form action="${TRACKING_URL}" method="get" target="_blank">`,
    `  <input name="id" placeholder="${placeholder}" required />`,
    ...(brand ? ['  <input type="hidden" name="brand" value="1" />'] : []),
    `  <button type="submit">${submit}</button>`,
    '</form>'
  ].join('\n');

/** Hướng dẫn gắn link / ô tra cứu vận đơn vào website của shop, đối tác. */
export default function ApiTrackingPage() {
  const { t } = useI18n();
  const copy = useCopyToClipboard();
  const [bill, setBill] = useState('');
  const [brand, setBrand] = useState(false);
  const session = useSession();
  const customerCode = session.data?.status === 'authenticated' ? session.data.user.customerCode : '';
  // Your Track = trang MyTracking của khách theo mã khách; chưa có mã khách thì dùng link thương hiệu đại lý cũ.
  const link = brand && customerCode
    ? bill.trim() ? customerTrackingUrl(MYTRACKING_URL, customerCode, bill) : `${customerTrackingUrl(MYTRACKING_URL, customerCode)}{MA_BILL}`
    : trackingLink(bill.trim() || '{MA_BILL}', brand);
  const code = embedCode(brand, t('Nhập mã vận đơn'), t('Tra cứu'));

  return (
    <>
      <PageHeader
        title="API Tracking"
        description="Gắn link hoặc ô tra cứu vận đơn Việt An vào website, tin nhắn gửi khách."
        actions={<LinkButton to="/ecommerce?tab=conn" size="sm"><Icon name="key" size={15} /> {t('API key tạo đơn')}</LinkButton>}
      />
      <div className="page-stack">
        <Card title="1. Link tra cứu cho từng đơn">
          <div className={styles.row}>
            <TextField label="Mã VA Bill" placeholder="VD 6156979" value={bill} onChange={e => setBill(e.target.value)} />
            <label className={styles.check}>
              <input type="checkbox" checked={brand} onChange={e => setBrand(e.target.checked)} />
              {t('Hiển thị theo thương hiệu đại lý (Your Track)')}
            </label>
          </div>
          <div className={styles.linkBox}>
            <code>{link}</code>
            <Button size="sm" onClick={() => void copy(link, t('Đã sao chép link tra cứu'))}><Icon name="copy" size={15} /> {t('Sao chép')}</Button>
            {bill.trim() && <a className={styles.open} href={link} target="_blank" rel="noreferrer">{t('Mở thử')}</a>}
          </div>
          <p className={styles.hint}>
            {t('Thay')} <code>{'{MA_BILL}'}</code> {t('bằng mã bill thật khi gửi cho khách (SMS, email, tin nhắn sàn).')}
          </p>
        </Card>

        <Card title="2. Ô tra cứu trên website của bạn">
          <p className={styles.hint}>{t('Dán đoạn HTML dưới đây vào trang web; khách nhập mã bill và bấm Tra cứu sẽ mở trang theo dõi của Việt An.')}</p>
          <pre className={styles.code}>{code}</pre>
          <Button size="sm" onClick={() => void copy(code, t('Đã sao chép mã nhúng'))}><Icon name="copy" size={15} /> {t('Sao chép mã nhúng')}</Button>
        </Card>

        <Notice title="Cần lấy trạng thái đơn qua API?">
          {t('Dùng API key ở trang E-commerce → Kết nối, gọi')} <code>GET /v1/orders/{'{bill}'}</code>{' '}
          {t('hoặc đăng ký webhook để nhận cập nhật trạng thái tự động.')}
        </Notice>
      </div>
    </>
  );
}
