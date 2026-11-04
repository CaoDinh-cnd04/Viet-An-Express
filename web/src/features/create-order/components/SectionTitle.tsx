import { useI18n } from '@/shared/i18n';
import { Icon, type IconName } from '@/shared/ui';
import styles from './form.module.css';

/**
 * Tiêu đề khung kiểu thoáng: icon · tên đậm · mô tả nhỏ bên dưới (nếu có).
 * Mặc định icon trong ô vuông xanh nhạt (Chi tiết kiện hàng, Invoice); `plain` = icon nét mảnh không ô (khung bước 1).
 */
export function SectionTitle({ icon, title, sub, plain }: { icon: IconName; title: string; sub?: string; plain?: boolean }) {
  const { t } = useI18n();
  return (
    <span className={styles.sectionTitle}>
      <span className={plain ? styles.sectionIconPlain : styles.sectionIcon} aria-hidden="true"><Icon name={icon} size={plain ? 16 : 15} /></span>
      <span className={styles.sectionText}>
        <span>{t(title)}</span>
        {sub && <small>{t(sub)}</small>}
      </span>
    </span>
  );
}
