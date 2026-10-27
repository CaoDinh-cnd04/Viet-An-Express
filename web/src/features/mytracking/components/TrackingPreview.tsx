import { useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { Icon, SegmentedControl } from '@/shared/ui';
import type { MyTrackingConfig } from '../schema';
import { HomeButton, TrackingLayout } from './TrackingLayout';
import styles from '../pages/MyTrackingPage.module.css';

/** Bản xem trước bố cục; không tạo sự kiện vận chuyển giả. */
export function TrackingPreview({ config }: { config: MyTrackingConfig }) {
  const { t } = useI18n();
  const [mode, setMode] = useState<'desktop' | 'mobile'>('desktop');

  return <section className={`${styles.card} ${styles.previewCard}`}>
    <div className={styles.previewHeader}>
      <h2>{t('Xem trước MyTracking')}</h2>
      <SegmentedControl ariaLabel={t('Chế độ xem trước')} value={mode} onChange={setMode}
        options={[{ value: 'desktop', label: 'Máy tính' }, { value: 'mobile', label: 'Điện thoại' }]} />
    </div>
    <div className={styles.previewViewport}>
      <TrackingLayout config={config} mobile={mode === 'mobile'}>
        <div className={styles.trackingSearch}>
          <input aria-label="Tracking number" placeholder="Tracking number" readOnly />
          <button type="button" disabled>Track</button>
          <HomeButton website={config.brand.website} preview />
        </div>
        <h3 className={styles.blackBar}>TRACKING DETAIL</h3>
        <div className={styles.trackingPlaceholder}>
          <Icon name="box" size={26} />
          <p>{t('Kết quả tra cứu sẽ hiển thị tại đây')}</p>
          <span>{t('Bản xem trước bố cục · chưa tra cứu vận đơn')}</span>
        </div>
      </TrackingLayout>
    </div>
  </section>;
}
