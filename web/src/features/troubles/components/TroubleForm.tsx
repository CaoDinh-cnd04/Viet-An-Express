import { useState, type ReactNode } from 'react';
import { useSession } from '@/features/auth';
import { useI18n } from '@/shared/i18n';
import { useCreateTrouble } from '../api';
import { FILE_ACCEPT, FILE_MAX_MB, SUPPORT_TOPICS, TROUBLE_KINDS, TROUBLE_PRIORITY, TROUBLE_TYPES } from '../constants';
import { toNewTrouble, troubleFormError, type TroubleFormValues } from '../lib/form';
import type { TroubleKind, TroublePriority, TroubleTicket } from '../types';
import styles from './TroubleForm.module.css';

interface TroubleFormProps {
  kind: TroubleKind;
  /** Điền sẵn: mã vận đơn (mở từ đơn hàng), chủ đề (vd "Sửa thông tin người gửi"). */
  bill?: string;
  topic?: string;
  onSent: (ticket: TroubleTicket) => void;
  onCancel: () => void;
}

const PRIORITIES = Object.keys(TROUBLE_PRIORITY) as TroublePriority[];

/** Form gửi yêu cầu ngay trên trang Hỗ trợ & sự cố — nhãn bên trái, ô nhập bên phải; trường thay đổi theo loại yêu cầu. */
export function TroubleForm({ kind, bill = '', topic, onSent, onCancel }: TroubleFormProps) {
  const { t } = useI18n();
  const create = useCreateTrouble();
  const session = useSession();
  const user = session.data?.status === 'authenticated' ? session.data.user : undefined;
  const [values, setValues] = useState<TroubleFormValues>(() => ({
    bill,
    type: topic ?? (kind === 'su_co' ? TROUBLE_TYPES[0] : kind === 'ho_tro' ? SUPPORT_TOPICS[0] : ''),
    subject: '',
    lv: 'mid',
    desc: '',
    req: user?.fullName || user?.contactName || '',
    contact: user?.phone || user?.email || ''
  }));
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState('');
  const set = (patch: Partial<TroubleFormValues>) => setValues(v => ({ ...v, ...patch }));

  const pickFile = (f: File | undefined) => {
    if (f && f.size > FILE_MAX_MB * 1024 * 1024) {
      setFile(null);
      return setError(t('Tệp tối đa {mb}MB', { mb: FILE_MAX_MB }));
    }
    setError('');
    setFile(f ?? null);
  };
  const submit = () => {
    const problem = troubleFormError(kind, values);
    if (problem) return setError(t(problem));
    setError('');
    create.mutate(toNewTrouble(kind, values, file), { onSuccess: res => onSent(res.data) });
  };

  return (
    <form className={styles.form} onSubmit={e => { e.preventDefault(); submit(); }} noValidate>
      {kind === 'su_co' && (
        <>
          <Row label="Mã vận đơn" required>
            <input value={values.bill} onChange={e => set({ bill: e.target.value })} placeholder="6179215" autoFocus={!bill} />
          </Row>
          <Row label="Vấn đề" required>
            <select value={values.type} onChange={e => set({ type: e.target.value })}>
              {TROUBLE_TYPES.map(x => <option key={x} value={x}>{t(x)}</option>)}
            </select>
          </Row>
          <Row label="Mức độ">
            <select value={values.lv} onChange={e => set({ lv: e.target.value as TroublePriority })}>
              {PRIORITIES.map(p => <option key={p} value={p}>{t(TROUBLE_PRIORITY[p].label)}</option>)}
            </select>
          </Row>
        </>
      )}
      {kind === 'ho_tro' && (
        <>
          <Row label="Chủ đề" required>
            <select value={values.type} onChange={e => set({ type: e.target.value })}>
              {SUPPORT_TOPICS.map(x => <option key={x} value={x}>{t(x)}</option>)}
            </select>
          </Row>
          <Row label="Tiêu đề" required>
            <input value={values.subject} onChange={e => set({ subject: e.target.value })} maxLength={200} placeholder={t('VD: Sửa địa chỉ người gửi')} autoFocus />
          </Row>
        </>
      )}
      <Row label={kind === 'su_co' ? 'Mô tả' : 'Nội dung'} required>
        <textarea
          value={values.desc}
          onChange={e => set({ desc: e.target.value })}
          maxLength={4000}
          autoFocus={kind === 'gop_y' || (kind === 'su_co' && !!bill)}
          placeholder={t(kind === 'su_co' ? 'Chuyện gì xảy ra, bạn muốn xử lý thế nào' : kind === 'ho_tro' ? 'Mô tả chi tiết điều bạn cần hỗ trợ…' : 'Điều bạn thấy chưa tiện, ý tưởng cải thiện…')}
        />
      </Row>
      <Row label="Đính kèm" hint={t('Ảnh / chứng từ, tối đa {mb}MB — gửi kèm email cho CS', { mb: FILE_MAX_MB })}>
        <input type="file" accept={FILE_ACCEPT} onChange={e => pickFile(e.target.files?.[0])} />
      </Row>
      {kind === 'su_co' && (
        <Row label="Người yêu cầu" required>
          <input value={values.req} onChange={e => set({ req: e.target.value })} />
        </Row>
      )}
      <Row label="SĐT / email" required={kind !== 'gop_y'}>
        <input value={values.contact} onChange={e => set({ contact: e.target.value })} placeholder={kind === 'gop_y' ? t('Không bắt buộc') : undefined} />
      </Row>
      <div className={styles.actions}>
        {error && <p className={styles.error} role="alert">{error}</p>}
        <button type="submit" className={styles.primary} disabled={create.isPending}>
          {t(create.isPending ? 'Đang gửi…' : kind === 'gop_y' ? 'Gửi góp ý' : 'Gửi yêu cầu')}
        </button>
        <button type="button" className={styles.secondary} onClick={onCancel}>{t('Hủy')}</button>
        <span className={styles.note}>{t(TROUBLE_KINDS[kind].label)} · {t('CS Việt An báo kết quả qua Thông báo, điện thoại hoặc email')}</span>
      </div>
    </form>
  );
}

function Row({ label, required, hint, children }: { label: string; required?: boolean; hint?: string; children: ReactNode }) {
  const { t } = useI18n();
  return (
    <label className={styles.row}>
      <span className={styles.label}>{t(label)}{required && <i aria-hidden="true"> *</i>}</span>
      <span className={styles.control}>
        {children}
        {hint && <small>{hint}</small>}
      </span>
    </label>
  );
}
