import { useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { Button, Icon } from '@/shared/ui';
import { useAnswerSupportTicket } from '../api';
import styles from './SupportAnswerForm.module.css';

const MAX = 4000;

/** Khách trả lời khi CS đang "Chờ khách phản hồi" — gửi xong yêu cầu chuyển lại "Đang xử lý". */
export function SupportAnswerForm({ id }: { id: string }) {
  const { t } = useI18n();
  const answer = useAnswerSupportTicket();
  const [text, setText] = useState('');
  const send = () => text.trim() && answer.mutate({ id, text }, { onSuccess: () => setText('') });
  return (
    <div className={styles.box}>
      <label className={styles.label} htmlFor={`answer-${id}`}>{t('CS đang chờ bạn phản hồi')}</label>
      <textarea id={`answer-${id}`} className={styles.input} rows={3} maxLength={MAX} value={text} onChange={e => setText(e.target.value)}
        placeholder={t('Trả lời CS Việt An…')} />
      <div className={styles.actions}>
        <Button variant="primary" size="sm" disabled={!text.trim() || answer.isPending} onClick={send}>
          <Icon name="send" size={13} /> {t(answer.isPending ? 'Đang gửi…' : 'Gửi phản hồi')}
        </Button>
      </div>
    </div>
  );
}
