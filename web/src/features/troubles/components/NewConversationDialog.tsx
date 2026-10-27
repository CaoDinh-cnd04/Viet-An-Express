import { useState } from 'react';
import { useSession } from '@/features/auth';
import { useOrders } from '@/features/orders/api';
import { DEFAULT_FILTERS } from '@/features/orders/constants';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Icon, Modal } from '@/shared/ui';
import { useCreateTrouble } from '../api';
import { FEEDBACK_TOPICS, FILE_ACCEPT, FILE_MAX_MB, HELP_TOPICS, TROUBLE_KIND_ORDER } from '../constants';
import { draftError, toNewTrouble, type ConversationDraft } from '../lib/conversation';
import type { TroubleKind, TroubleTicket } from '../types';
import styles from './NewConversationDialog.module.css';

/** Nhãn ngắn trên ô màu của từng loại (giống ảnh đại diện trong danh sách trò chuyện). */
export const KIND_BADGE: Record<TroubleKind, string> = { su_co: 'SC', ho_tro: 'HT', gop_y: 'GY' };

const KIND_CHOICE: Record<TroubleKind, { label: string; hint: string }> = {
  su_co: { label: 'Hỗ trợ & sự cố', hint: 'Sự cố đơn hàng, tracking, cước, tạo đơn, sửa thông tin…' },
  ho_tro: { label: 'Hỗ trợ & sự cố', hint: 'Sự cố đơn hàng, tracking, cước, tạo đơn, sửa thông tin…' },
  gop_y: { label: 'Góp ý', hint: 'Dịch vụ, nhân viên, phần mềm' }
};

const RATES = ['Rất tệ', 'Tệ', 'Tạm', 'Tốt', 'Rất tốt'];

interface NewConversationDialogProps {
  open: boolean;
  /** Mở sẵn loại / vận đơn / chủ đề (từ đơn hàng, form tạo đơn). */
  preset?: { kind?: TroubleKind; bill?: string; topic?: string };
  onClose: () => void;
  onSent: (ticket: TroubleTicket) => void;
}

/**
 * Tạo cuộc trò chuyện mới, 2 bước: chọn loại → điền gọn (hỗ trợ & sự cố: vấn đề + mã vận đơn nếu có + nội dung — có mã vận đơn thì
 * nội dung không bắt buộc; góp ý: mức hài lòng + nội dung). Người liên hệ lấy từ tài khoản, chỉ hỏi khi tài khoản chưa có SĐT / email.
 * Lưu dbo.TContact như form "Yêu cầu" của Bill Online cũ.
 */
export function NewConversationDialog({ open, preset, onClose, onSent }: NewConversationDialogProps) {
  const { t } = useI18n();
  const session = useSession();
  const user = session.data?.status === 'authenticated' ? session.data.user : undefined;
  const create = useCreateTrouble();
  const [draft, setDraft] = useState<ConversationDraft | null>(() => (preset?.kind ? start(preset.kind) : null));
  const help = draft?.kind !== 'gop_y';
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState('');

  function start(picked: TroubleKind): ConversationDraft {
    // Sự cố và hỗ trợ là 1 loại (nút "Báo sự cố" ở đơn hàng mở sẵn su_co).
    const kind: TroubleKind = picked === 'su_co' ? 'ho_tro' : picked;
    return {
      kind,
      bill: kind === 'ho_tro' ? preset?.bill ?? '' : '',
      topic: preset?.topic ?? '',
      rating: null,
      text: '',
      urgent: false,
      requester: user?.fullName || user?.contactName || '',
      contact: user?.phone || user?.email || ''
    };
  }
  const set = (patch: Partial<ConversationDraft>) => setDraft(d => (d ? { ...d, ...patch } : d));

  const recent = useOrders({ ...DEFAULT_FILTERS, pageSize: 5 });
  const typed = help ? draft?.bill.trim() ?? '' : '';
  const lookup = useOrders({ ...DEFAULT_FILTERS, q: typed, searchField: 'bill', pageSize: 1 });
  const found = typed ? lookup.data?.items.find(o => o.bill === typed) : undefined;

  const pickFile = (f: File | undefined) => {
    setFileError('');
    if (f && f.size > FILE_MAX_MB * 1024 * 1024) return setFileError(t('Tệp lớn hơn {n} MB', { n: FILE_MAX_MB }));
    setFile(f ?? null);
  };

  const error = draft ? draftError(draft) : null;
  const send = () => {
    if (!draft || error) return;
    create.mutate(toNewTrouble(draft, file), { onSuccess: res => onSent(res.data) });
  };

  const title = draft ? KIND_CHOICE[draft.kind].label : 'Bạn cần gì?';

  return (
    <Modal
      open={open}
      title={title}
      onClose={onClose}
      footer={
        draft ? (
          <>
            <Button variant="ghost" onClick={onClose}>{t('Hủy')}</Button>
            <Button variant="primary" onClick={send} disabled={!!error || create.isPending} title={error ? t(error) : undefined}>
              <Icon name="send" size={15} /> {t(create.isPending ? 'Đang gửi…' : 'Gửi')}
            </Button>
          </>
        ) : (
          <Button variant="ghost" onClick={onClose}>{t('Hủy')}</Button>
        )
      }
    >
      {!draft ? (
        <div className={styles.kinds}>
          {TROUBLE_KIND_ORDER.map(k => (
            <button key={k} type="button" className={styles.kind} onClick={() => setDraft(start(k))}>
              <span className={cx(styles.badge, styles[k])}>{KIND_BADGE[k]}</span>
              <span>
                <b>{t(KIND_CHOICE[k].label)}</b>
                <small>{t(KIND_CHOICE[k].hint)}</small>
              </span>
              <Icon name="chevronRight" size={16} />
            </button>
          ))}
        </div>
      ) : (
        <div className={styles.form}>
          {!preset?.kind && (
            <button type="button" className={styles.back} onClick={() => setDraft(null)}>
              ‹ {t('Đổi loại')}
            </button>
          )}

          {help && (
            <label className={styles.field}>
              {t('Mã vận đơn')} <small>· {t('không bắt buộc')}</small>
              <input className={cx(styles.input, 'mono')} value={draft.bill} onChange={e => set({ bill: e.target.value })} placeholder={t('Vd: 94012388')} autoFocus />
              {!!recent.data?.items.length && (
                <span className={styles.recent}>
                  {t('Đơn gần đây:')}
                  {recent.data.items.map(o => (
                    <button key={o.bill} type="button" className="mono" onClick={() => set({ bill: o.bill })}>{o.bill}</button>
                  ))}
                </span>
              )}
              {found && <span className={styles.found}><Icon name="check" size={13} /> {found.cnee} · {found.ct} · {found.pcs}</span>}
            </label>
          )}

          <div className={styles.field}>
            {t(help ? 'Vấn đề' : 'Loại góp ý')}
            <div className={styles.opts}>
              {(help ? HELP_TOPICS : FEEDBACK_TOPICS).map(x => (
                <button key={x} type="button" className={cx(styles.opt, draft.topic === x && styles.on)} aria-pressed={draft.topic === x} onClick={() => set({ topic: x })}>
                  {t(x)}
                </button>
              ))}
            </div>
          </div>

          {!help && (
            <div className={styles.field}>
              {t('Bạn hài lòng với Việt An ở mức nào?')}
              <div className={styles.rate}>
                {RATES.map((label, i) => (
                  <button key={label} type="button" className={cx(draft.rating === i + 1 && styles.on)} aria-pressed={draft.rating === i + 1}
                    onClick={() => set({ rating: draft.rating === i + 1 ? null : i + 1 })}>
                    {i + 1}<small>{t(label)}</small>
                  </button>
                ))}
              </div>
            </div>
          )}

          <label className={styles.field}>
            {t(draft.kind === 'gop_y' ? 'Góp ý của bạn' : 'Nội dung')}
            {help && draft.bill.trim() && <small> · {t('không bắt buộc')}</small>}
            <textarea className={styles.input} rows={4} value={draft.text} onChange={e => set({ text: e.target.value })}
              placeholder={t(draft.kind === 'gop_y' ? 'Điều bạn thích, điều cần cải thiện…' : 'Viết ngắn gọn điều bạn cần…')} />
          </label>

          {help && !(user?.phone || user?.email) && (
            <label className={styles.field}>
              {t('SĐT hoặc email để CS liên hệ')}
              <input className={styles.input} value={draft.contact} onChange={e => set({ contact: e.target.value })} />
            </label>
          )}

          <div className={styles.line}>
            <label className={styles.attach}>
              <Icon name="image" size={14} /> {file ? file.name : t('Ảnh / tệp')} <small>≤ {FILE_MAX_MB} MB</small>
              <input type="file" accept={FILE_ACCEPT} hidden onChange={e => pickFile(e.target.files?.[0])} />
            </label>
            {help && (
              <label className={styles.urgent}>
                <input type="checkbox" checked={draft.urgent} onChange={e => set({ urgent: e.target.checked })} /> {t('Cần xử lý gấp')}
              </label>
            )}
          </div>
          {fileError && <p className={styles.error}>{fileError}</p>}
          {help && draft.contact && (
            <p className={styles.who}>{t('CS liên hệ: {who}', { who: [draft.requester, draft.contact].filter(Boolean).join(' · ') })}</p>
          )}
        </div>
      )}
    </Modal>
  );
}
