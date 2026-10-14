import { useI18n } from '@/shared/i18n';
import { SENDER_FIELDS, type SenderInfo, type SenderRequest } from './types';
import styles from './SenderInfo.module.css';
export function SenderComparison({ request, live }: { request: SenderRequest; live?: SenderInfo | null }) {
  const { t } = useI18n();
  return <div className={styles.scroll}><table className={styles.table}>
    <thead><tr><th>{t('Thông tin')}</th><th>{t('Trước khi sửa')}</th><th>{t('Đề nghị mới')}</th>{live && <th>{t('Hồ sơ hiện tại')}</th>}</tr></thead>
    <tbody>{SENDER_FIELDS.map(f => <tr key={f.key} className={request.changed.includes(f.key) ? styles.changed : undefined}>
      <th scope="row">{t(f.label)}{request.changed.includes(f.key) && <small>{t('Có thay đổi')}</small>}</th>
      <td>{request.old[f.key] || '—'}</td><td>{request.new[f.key] || '—'}</td>{live && <td>{live[f.key] || '—'}</td>}
    </tr>)}</tbody>
  </table></div>;
}
