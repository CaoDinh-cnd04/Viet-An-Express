import { useI18n } from '@/shared/i18n';
import { Card, LinkButton, PageHeader } from '@/shared/ui';
import { TermsButton } from '@/features/terms';
import { useDrafts } from '../api';
import { DraftsTable } from '../components/DraftsTable';
import styles from './DraftsPage.module.css';

export default function DraftsPage() {
  const { data: drafts = [] } = useDrafts();
  const { t } = useI18n();

  return (
    <>
      <PageHeader
        title="Đơn nháp & chưa in"
        description="Đơn chỉ được cấp mã bill Việt An khi bấm In. Sau khi in, đơn được khóa và chuyển sang Đơn hàng của tôi."
        actions={
          <>
            <TermsButton />
            <LinkButton to="/orders/new" variant="primary" size="sm">{t('Tạo đơn mới')}</LinkButton>
          </>
        }
      />
      <Card flush>
        <DraftsTable />
        <footer className={styles.footer}>{t('{n} đơn nháp / chưa in', { n: drafts.length })}</footer>
      </Card>
    </>
  );
}
