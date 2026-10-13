import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useSession } from '@/features/auth';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Icon, Modal } from '@/shared/ui';
import { useCreateTrouble } from '../api';
import { TROUBLE_PRIORITY, TROUBLE_SUGGESTIONS, TROUBLE_TYPES } from '../constants';
import { hasSentence, toggleSentence } from '../lib/report';
import type { TroublePriority } from '../types';
import styles from './ReportTroubleDialog.module.css';

/** Thông tin đơn điền sẵn khi mở từ "Đơn hàng của tôi". */
export interface TroubleContext {
  bill: string;
  cnee?: string;
  ct?: string;
}

interface ReportTroubleDialogProps {
  open: boolean;
  context?: TroubleContext | null;
  onClose: () => void;
}

type Step = 'bill' | 'type' | 'desc' | 'lv' | 'contact';
type TroubleType = (typeof TROUBLE_TYPES)[number];

const DESC_MIN = 10;
const PRIORITIES = Object.keys(TROUBLE_PRIORITY) as TroublePriority[];

/**
 * Báo sự cố dạng hộp chat: CS Việt An hỏi từng bước (vận đơn → loại sự cố → mô tả → mức độ → liên hệ),
 * khách bấm câu trả lời gợi ý thay vì gõ. Bấm "Sửa" ở câu trả lời để quay lại bước đó.
 * Dùng ở trang Sự cố và từ đơn trong "Đơn hàng của tôi".
 */
export function ReportTroubleDialog({ open, context, onClose }: ReportTroubleDialogProps) {
  const { t } = useI18n();
  const create = useCreateTrouble();
  const session = useSession();
  const user = session.data?.status === 'authenticated' ? session.data.user : undefined;

  const ctxBill = context?.bill ?? '';
  const [step, setStep] = useState<Step>('type');
  const [bill, setBill] = useState('');
  const [type, setType] = useState<TroubleType | ''>('');
  const [desc, setDesc] = useState('');
  const [lv, setLv] = useState<TroublePriority | ''>('');
  const [req, setReq] = useState('');
  const [contact, setContact] = useState('');
  const [error, setError] = useState('');
  const endRef = useRef<HTMLDivElement>(null);

  // Mở hộp hoặc đổi đơn: bắt đầu lại; người yêu cầu / liên hệ điền sẵn từ tài khoản
  useEffect(() => {
    if (!open) return;
    setStep(ctxBill ? 'type' : 'bill');
    setBill(ctxBill);
    setType('');
    setDesc('');
    setLv('');
    setReq(user?.fullName || user?.contactName || '');
    setContact(user?.phone || user?.email || '');
    setError('');
    // eslint-disable-next-line react-hooks/exhaustive-deps -- chỉ làm mới khi mở hộp / đổi đơn
  }, [open, ctxBill]);

  // Luôn cuộn tới câu hỏi mới nhất (thân hàm có ngoặc: scrollIntoView ở trình duyệt mới trả Promise, effect không được trả giá trị)
  useEffect(() => {
    endRef.current?.scrollIntoView({ block: 'nearest' });
  }, [step]);

  const order: Step[] = ctxBill ? ['type', 'desc', 'lv', 'contact'] : ['bill', 'type', 'desc', 'lv', 'contact'];
  const at = order.indexOf(step);
  const done = (s: Step) => order.indexOf(s) < at;
  const next = () => {
    setError('');
    setStep(order[at + 1] ?? step);
  };

  const confirmBill = () => (bill.trim() ? next() : setError(t('Nhập mã vận đơn')));
  const confirmDesc = () => (desc.trim().length >= DESC_MIN ? next() : setError(t('Mô tả tối thiểu 10 ký tự để CS xử lý nhanh')));
  const submit = () => {
    if (!req.trim()) return setError(t('Nhập người yêu cầu'));
    if (!contact.trim()) return setError(t('Nhập SĐT hoặc email liên hệ'));
    setError('');
    create.mutate(
      { bill: bill.trim(), cnee: context?.cnee, ct: context?.ct, type: type || 'Khác', lv: lv || 'mid', desc: desc.trim(), req: req.trim(), contact: contact.trim() },
      { onSuccess: onClose }
    );
  };

  const edit = (s: Step) => () => {
    setError('');
    setStep(s);
  };

  return (
    <Modal open={open} title="Báo cáo sự cố đơn hàng" onClose={onClose} footerNote="Yêu cầu được gửi tới bộ phận CS Việt An">
      {bill.trim() && (
        <div className={styles.order}>
          <Icon name="box" size={15} />
          <span>{t('Vận đơn')}</span>
          <strong className="mono">{bill.trim()}</strong>
          {context?.cnee && <span className={styles.orderSub}>· {context.cnee}{context.ct ? ` · ${context.ct}` : ''}</span>}
        </div>
      )}

      <div className={styles.chat}>
        <Bot>{t('Xin chào, CS Việt An sẽ giúp bạn xử lý sự cố. Chỉ cần trả lời vài câu hỏi dưới đây.')}</Bot>

        {!ctxBill && (
          <>
            <Bot>{t('Bạn cần hỗ trợ đơn nào? Nhập mã vận đơn (VA Bill).')}</Bot>
            {done('bill') ? (
              <Me onEdit={edit('bill')}><span className="mono">{bill.trim()}</span></Me>
            ) : step === 'bill' && (
              <div className={styles.inputRow}>
                <input autoFocus className={styles.input} value={bill} onChange={e => setBill(e.target.value)} onKeyDown={e => e.key === 'Enter' && confirmBill()} placeholder="VAHCM…" aria-label={t('Mã vận đơn (VA Bill)')} />
                <Button variant="primary" size="sm" onClick={confirmBill}>{t('Tiếp tục')}</Button>
              </div>
            )}
          </>
        )}

        {at >= order.indexOf('type') && (
          <>
            <Bot>{t('Đơn đang gặp vấn đề gì?')}</Bot>
            {done('type') ? (
              <Me onEdit={edit('type')}>{t(type)}</Me>
            ) : (
              <div className={styles.chips}>
                {TROUBLE_TYPES.map(x => (
                  <button key={x} type="button" className={styles.chip} onClick={() => { setType(x); next(); }}>{t(x)}</button>
                ))}
              </div>
            )}
          </>
        )}

        {at >= order.indexOf('desc') && type && (
          <>
            <Bot>{t('Bạn chọn nhanh các ý dưới đây hoặc gõ thêm chi tiết nhé.')}</Bot>
            {done('desc') ? (
              <Me onEdit={edit('desc')}>{desc.trim()}</Me>
            ) : (
              <div className={styles.answer}>
                <div className={styles.chips}>
                  {TROUBLE_SUGGESTIONS[type].map(s => {
                    const on = hasSentence(desc, t(s));
                    return (
                      <button key={s} type="button" aria-pressed={on} className={cx(styles.chip, on && styles.chipOn)} onClick={() => setDesc(d => toggleSentence(d, t(s)))}>
                        {on ? <Icon name="check" size={13} /> : <Icon name="plus" size={13} />} {t(s)}
                      </button>
                    );
                  })}
                </div>
                <textarea className={styles.textarea} rows={3} value={desc} onChange={e => setDesc(e.target.value)} placeholder={t('Điều gì xảy ra, khi nào, bạn mong muốn xử lý thế nào…')} aria-label={t('Mô tả chi tiết')} />
                <div className={styles.actions}>
                  <Button variant="primary" size="sm" onClick={confirmDesc}>{t('Tiếp tục')}</Button>
                </div>
              </div>
            )}
          </>
        )}

        {at >= order.indexOf('lv') && (
          <>
            <Bot>{t('Mức độ cần xử lý?')}</Bot>
            {done('lv') && lv ? (
              <Me onEdit={edit('lv')}>{t(TROUBLE_PRIORITY[lv].label)}</Me>
            ) : (
              <div className={styles.chips}>
                {PRIORITIES.map(p => (
                  <button key={p} type="button" className={cx(styles.chip, styles[`lv_${p}`])} onClick={() => { setLv(p); next(); }}>{t(TROUBLE_PRIORITY[p].label)}</button>
                ))}
              </div>
            )}
          </>
        )}

        {step === 'contact' && (
          <>
            <Bot>{t('CS sẽ liên hệ ai khi có kết quả? Kiểm tra lại giúp mình rồi bấm Gửi.')}</Bot>
            <div className={styles.answer}>
              <div className={styles.contact}>
                <label>
                  <span>{t('Người yêu cầu')}</span>
                  <input className={styles.input} value={req} onChange={e => setReq(e.target.value)} />
                </label>
                <label>
                  <span>{t('SĐT / email liên hệ')}</span>
                  <input className={styles.input} value={contact} onChange={e => setContact(e.target.value)} />
                </label>
              </div>
              <div className={styles.actions}>
                <Button variant="primary" onClick={submit} disabled={create.isPending}>
                  <Icon name="send" size={15} /> {t(create.isPending ? 'Đang gửi…' : 'Gửi yêu cầu')}
                </Button>
              </div>
            </div>
          </>
        )}

        {error && <p className={styles.error} role="alert">{error}</p>}
        <div ref={endRef} />
      </div>
    </Modal>
  );
}

/** Tin của CS Việt An (bên trái, có avatar). */
function Bot({ children }: { children: ReactNode }) {
  return (
    <div className={styles.bot}>
      <span className={styles.avatar} aria-hidden="true">VA</span>
      <div className={styles.botBubble}>{children}</div>
    </div>
  );
}

/** Câu trả lời của khách (bên phải) + nút sửa để quay lại bước đó. */
function Me({ children, onEdit }: { children: ReactNode; onEdit: () => void }) {
  const { t } = useI18n();
  return (
    <div className={styles.me}>
      <button type="button" className={styles.edit} onClick={onEdit}>{t('Sửa')}</button>
      <div className={styles.meBubble}>{children}</div>
    </div>
  );
}
