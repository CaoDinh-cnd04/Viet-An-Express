import { useSession } from '@/features/auth';
import { useI18n } from '@/shared/i18n';
import { Notice } from '@/shared/ui';
import { ConfigForm } from '../components/ConfigForm';
import { PublishPanel } from '../components/PublishPanel';
import { TrackingPreview } from '../components/TrackingPreview';
import { useMyTrackingConfig } from '../hooks/useMyTrackingConfig';
import styles from './MyTrackingPage.module.css';
export default function MyTrackingPage() {
  const session = useSession();
  if (session.isPending) return null;
  const userId = session.data?.status === 'authenticated' ? session.data.user.customerCode : 'preview';
  return <MyTrackingEditor key={userId} userId={userId} />;
}
function MyTrackingEditor({ userId }: { userId: string }) {
  const { t } = useI18n();
  const state = useMyTrackingConfig(userId);
  const busy = state.loading || state.saving;
  return <div className={styles.page}>
    <header className={styles.pageHeader}><h1>{t('MyTracking cá nhân')}</h1><p>{t('Tùy chỉnh nội dung và hình ảnh trang tra cứu của bạn.')}</p></header>
    {!state.loading && <PublishPanel publication={state.publication} />}
    {state.feedback && <Notice tone={state.feedback.warning ? 'warning' : 'info'}>{t(state.feedback.message)}</Notice>}
    <div className={styles.layout}>
      <ConfigForm form={state.form} onSave={state.save} onRestore={state.restoreDefaults} disabled={busy} />
      <TrackingPreview config={state.config} />
    </div>
  </div>;
}
