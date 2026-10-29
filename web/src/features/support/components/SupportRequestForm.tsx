import { useState } from 'react';
import { useSession } from '@/features/auth';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Icon } from '@/shared/ui';
import { useBillLookup, useCreateSupportRequest, useRecentBills } from '../api';
import { FEEDBACK_TOPICS, FILE_ACCEPT, FILE_MAX_MB, HELP_TOPICS, SUPPORT_KIND_ORDER, SUPPORT_KINDS } from '../constants';
import { draftError, newDraft, toNewSupportRequest, type RequestDraft, type RequestPreset } from '../lib/draft';
import type { SupportTicket } from '../types';
import styles from './SupportRequestForm.module.css';

const RATES = ['Rất tệ', 'Tệ', 'Tạm', 'Tốt', 'Rất tốt'];

interface SupportRequestFormProps {
  /** Mở sẵn loại / vận đơn / vấn đề. Đổi preset thì đổi `key` của form để điền lại. */
  preset?: RequestPreset;
  /** Gửi xong (form tự trống lại). */
  onSent?: (ticket: SupportTicket) => void;
  /** Có thì hiện nút Hủy (dùng trong hộp thoại). */
  onCancel?: () => void;
}

/**
 * Form "Gửi yêu cầu mới" 1 cột, nhãn bên trái — lưu dbo.TContact như form "Yêu cầu" của Bill Online cũ.
 * Hỗ trợ & sự cố: mã vận đơn không bắt buộc (gợi ý đơn gần đây, hiện người nhận khi tra đúng); có mã thì nội dung không bắt buộc.
 */
export function SupportRequestForm({ preset = {}, onSent, onCancel }: SupportRequestFormProps) {
  const { t } = useI18n();
  const session = useSession();
  const user = session.data?.status === 'authenticated' ? session.data.user : undefined;
  const account = { requester: user?.fullName || user?.contactName || '', contact: user?.phone || user?.email || '' };
  const create = useCreateSupportRequest();
  const [draft, setDraft] = useState<RequestDraft>(() => newDraft(preset, account));
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState('');
  const [editContact, setEditContact] = useState(!account.contact);
  const [tried, setTried] = useState(false);
  const help = draft.kind !== 'gop_y';
  const set = (patch: Partial<RequestDraft>) => setDraft(d => ({ ...d, ...patch }));

  const recent = useRecentBills();
  const found = useBillLookup(help ? draft.bill.trim() : '').data;

  const pickFile = (f: File | undefined) => {
    setFileError('');
    if (f && f.size > FILE_MAX_MB * 1024 * 1024) return setFileError(t('Tệp lớn hơn {n} MB', { n: FILE_MAX_MB }));
    setFile(f ?? null);
  };

  const error = draftError(draft);
  const send = () => {
    setTried(true);
    if (error) return;
    create.mutate(toNewSupportRequest(draft, file), {
      onSuccess: res => {
        setDraft(newDraft({ kind: draft.kind }, account));
        setFile(null);
        setTried(false);
        onSent?.(res.data);
      }
    });
  };

  return (
    <form className={styles.form} onSubmit={e => { e.preventDefault(); send(); }} noValidate>
      <div className={styles.row}>
        <span className={styles.label}>{t('Loại')}</span>
        <span className={styles.seg} role="radiogroup" aria-label={t('Loại yêu cầu')}>
          {SUPPORT_KIND_ORDER.map(k => (
            <button key={k} type="button" role="radio" aria-checked={draft.kind === k} className={cx(draft.kind === k && styles.on)}
              onClick={() => setDraft(d => ({ ...newDraft({ kind: k }, { requester: d.requester, contact: d.contact }), text: d.text }))}>
              {t(SUPPORT_KINDS[k].label)}
            </button>
          ))}
        </span>
      </div>

      {help && (
        <div className={styles.row}>
          <label className={styles.label} htmlFor="support-bill">{t('Mã vận đơn')}</label>
          <div>
            <input id="support-bill" className={cx(styles.input, styles.bill)} value={draft.bill} onChange={e => set({ bill: e.target.value })}
              placeholder={t('Không bắt buộc — vd 6179215')} />
            {found && <div className={styles.found}><Icon name="check" size={12} /> {found.cnee} · {found.ct} · {found.pcs}</div>}
            {!!recent.data?.length && (
              <div className={styles.chips}>
                <span>{t('Đơn gần đây:')}</span>
                {recent.data.map(o => (
                  <button key={o.bill} type="button" className="mono" onClick={() => set({ bill: o.bill })}>{o.bill}</button>
                ))}
              </div>
            )}
          </div>
        </div>
      )}

      <div className={styles.row}>
        <label className={styles.label} htmlFor="support-topic">{t(help ? 'Vấn đề' : 'Nội dung góp ý')} <b>*</b></label>
        <select id="support-topic" className={styles.input} value={draft.topic} onChange={e => set({ topic: e.target.value })}
          aria-invalid={tried && !draft.topic}>
          <option value="">{t(help ? '— Chọn vấn đề —' : '— Chọn nội dung góp ý —')}</option>
          {(help ? HELP_TOPICS : FEEDBACK_TOPICS).map(x => <option key={x} value={x}>{t(x)}</option>)}
        </select>
      </div>

      {!help && (
        <div className={styles.row}>
          <span className={styles.label}>{t('Mức hài lòng')}</span>
          <span className={styles.rate}>
            {RATES.map((label, i) => (
              <button key={label} type="button" title={t(label)} aria-pressed={draft.rating === i + 1} className={cx(draft.rating === i + 1 && styles.on)}
                onClick={() => set({ rating: draft.rating === i + 1 ? null : i + 1 })}>
                {i + 1}
              </button>
            ))}
          </span>
        </div>
      )}

      <div className={styles.row}>
        <label className={styles.label} htmlFor="support-text">{t('Nội dung')} {!(help && draft.bill.trim()) && <b>*</b>}</label>
        <textarea id="support-text" className={styles.input} rows={5} value={draft.text} onChange={e => set({ text: e.target.value })}
          placeholder={t(help ? 'Viết ngắn gọn điều bạn cần — có mã vận đơn thì có thể để trống' : 'Điều bạn thích, điều cần cải thiện…')} />
      </div>

      <div className={styles.row}>
        <span className={styles.label}>{t('Tệp đính kèm')}</span>
        <div>
          <label className={styles.file}>
            <Icon name="image" size={13} /> {file ? file.name : t('Chọn tệp…')}
            <input type="file" accept={FILE_ACCEPT} hidden onChange={e => pickFile(e.target.files?.[0])} />
          </label>
          <div className={styles.hint}>{t('Ảnh, PDF, Excel, Word — tối đa {n} MB, gửi kèm email cho CS', { n: FILE_MAX_MB })}</div>
          {fileError && <div className={styles.error}>{fileError}</div>}
        </div>
      </div>

      {help && (
        <div className={styles.row}>
          <label className={styles.label} htmlFor="support-contact">{t('Liên hệ')} <b>*</b></label>
          {editContact ? (
            <input id="support-contact" className={styles.input} value={draft.contact} onChange={e => set({ contact: e.target.value })}
              placeholder={t('SĐT hoặc email để CS liên hệ')} aria-invalid={tried && !draft.contact.trim()} />
          ) : (
            <span className={styles.value}>
              {[draft.requester, draft.contact].filter(Boolean).join(' · ')}{' '}
              <button type="button" className={styles.link} onClick={() => setEditContact(true)}>{t('(đổi)')}</button>
            </span>
          )}
        </div>
      )}

      <div className={styles.actions}>
        {help ? (
          <label className={styles.check}>
            <input type="checkbox" checked={draft.urgent} onChange={e => set({ urgent: e.target.checked })} /> {t('Cần xử lý gấp')}
          </label>
        ) : <span />}
        <span className={styles.buttons}>
          {tried && error && <span className={styles.error} role="alert">{t(error)}</span>}
          {onCancel && <Button variant="ghost" onClick={onCancel}>{t('Hủy')}</Button>}
          <Button variant="primary" type="submit" disabled={create.isPending}>
            <Icon name="send" size={14} /> {t(create.isPending ? 'Đang gửi…' : 'Gửi yêu cầu')}
          </Button>
        </span>
      </div>
    </form>
  );
}
