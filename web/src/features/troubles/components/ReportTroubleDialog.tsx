import { useEffect, useRef, useState, type ReactNode } from 'react';
import { useSession } from '@/features/auth';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Icon, Modal } from '@/shared/ui';
import { useCreateTrouble } from '../api';
import { FILE_ACCEPT, FILE_MAX_MB, SUPPORT_TOPICS, TROUBLE_KIND_ORDER, TROUBLE_KINDS, TROUBLE_PRIORITY, TROUBLE_SUGGESTIONS, TROUBLE_TYPES } from '../constants';
import { hasSentence, toggleSentence } from '../lib/report';
import type { TroubleKind, TroublePriority } from '../types';
import styles from './ReportTroubleDialog.module.css';

/** Thông tin đơn điền sẵn khi mở từ "Đơn hàng của tôi". */
export interface TroubleContext {
  bill: string;
  cnee?: string;
  ct?: string;
}

/** Mở sẵn loại / chủ đề (vd "Sửa thông tin người gửi" từ form tạo đơn). */
export interface TroublePreset {
  kind: TroubleKind;
  type?: string;
}

interface ReportTroubleDialogProps {
  open: boolean;
  context?: TroubleContext | null;
  preset?: TroublePreset | null;
  onClose: () => void;
}

type Step = 'kind' | 'bill' | 'type' | 'desc' | 'lv' | 'contact';
type TroubleType = (typeof TROUBLE_TYPES)[number];

const DESC_MIN = 10;
const SUBJECT_MIN = 5;
const PRIORITIES = Object.keys(TROUBLE_PRIORITY) as TroublePriority[];

/** Các bước hỏi theo loại yêu cầu; mở từ 1 đơn hàng thì bỏ bước chọn loại và nhập vận đơn. */
function stepsOf(kind: TroubleKind | '', fromOrder: boolean): Step[] {
  if (fromOrder) return ['type', 'desc', 'lv', 'contact'];
  if (kind === 'ho_tro') return ['kind', 'type', 'desc', 'contact'];
  if (kind === 'gop_y') return ['kind', 'desc', 'contact'];
  return ['kind', 'bill', 'type', 'desc', 'lv', 'contact'];
}

/**
 * Gửi yêu cầu cho CS Việt An dạng hộp chat: chọn loại (sự cố vận đơn / yêu cầu hỗ trợ / góp ý), CS hỏi từng bước,
 * khách bấm câu trả lời gợi ý thay vì gõ, đính kèm 1 tệp ở bước cuối. Bấm "Sửa" ở câu trả lời để quay lại bước đó.
 * Dùng ở trang Hỗ trợ & sự cố và từ đơn trong "Đơn hàng của tôi" (luôn là sự cố của đơn đó).
 */
export function ReportTroubleDialog({ open, context, preset, onClose }: ReportTroubleDialogProps) {
  const { t } = useI18n();
  const create = useCreateTrouble();
  const session = useSession();
  const user = session.data?.status === 'authenticated' ? session.data.user : undefined;

  const ctxBill = context?.bill ?? '';
  const [step, setStep] = useState<Step>('kind');
  const [kind, setKind] = useState<TroubleKind | ''>('');
  const [bill, setBill] = useState('');
  const [type, setType] = useState('');
  const [subject, setSubject] = useState('');
  const [desc, setDesc] = useState('');
  const [lv, setLv] = useState<TroublePriority | ''>('');
  const [req, setReq] = useState('');
  const [contact, setContact] = useState('');
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState('');
  const endRef = useRef<HTMLDivElement>(null);

  // Mở hộp hoặc đổi đơn: bắt đầu lại; người yêu cầu / liên hệ điền sẵn từ tài khoản
  useEffect(() => {
    if (!open) return;
    // Mở sẵn chủ đề: bỏ qua bước chọn loại / chủ đề, vào thẳng phần nội dung.
    // Bước đầu sau "chọn loại" của loại đó (sự cố: nhập vận đơn; hỗ trợ: chủ đề; góp ý: nội dung); có sẵn chủ đề thì vào thẳng nội dung.
    const start: Step = ctxBill ? 'type' : preset ? (preset.type ? 'desc' : stepsOf(preset.kind, false)[1] ?? 'desc') : 'kind';
    setStep(start);
    setKind(ctxBill ? 'su_co' : preset?.kind ?? '');
    setBill(ctxBill);
    setType(preset?.type ?? '');
    setSubject('');
    setDesc('');
    setLv('');
    setReq(user?.fullName || user?.contactName || '');
    setContact(user?.phone || user?.email || '');
    setFile(null);
    setError('');
    // eslint-disable-next-line react-hooks/exhaustive-deps -- chỉ làm mới khi mở hộp / đổi đơn
  }, [open, ctxBill, preset?.kind, preset?.type]);

  // Luôn cuộn tới câu hỏi mới nhất (thân hàm có ngoặc: scrollIntoView ở trình duyệt mới trả Promise, effect không được trả giá trị)
  useEffect(() => {
    endRef.current?.scrollIntoView({ block: 'nearest' });
  }, [step]);

  const order = stepsOf(kind, !!ctxBill);
  const at = order.indexOf(step);
  const shows = (s: Step) => order.includes(s) && at >= order.indexOf(s);
  const done = (s: Step) => order.includes(s) && order.indexOf(s) < at;
  const goTo = (s: Step) => {
    setError('');
    setStep(s);
  };
  const next = (from: Step = step, steps = order) => goTo(steps[steps.indexOf(from) + 1] ?? from);

  const pickKind = (k: TroubleKind) => {
    // Đổi loại: chủ đề / loại sự cố của loại cũ không còn hợp lệ
    if (k !== kind) setType('');
    setKind(k);
    next('kind', stepsOf(k, false));
  };
  const confirmBill = () => (bill.trim() ? next() : setError(t('Nhập mã vận đơn')));
  const confirmDesc = () => {
    if (kind === 'ho_tro' && subject.trim().length < SUBJECT_MIN) return setError(t('Tiêu đề tối thiểu 5 ký tự'));
    if (desc.trim().length < DESC_MIN) return setError(t('Nội dung tối thiểu 10 ký tự để CS xử lý nhanh'));
    next();
  };
  const pickFile = (f: File | undefined) => {
    if (f && f.size > FILE_MAX_MB * 1024 * 1024) {
      setFile(null);
      return setError(t('Tệp tối đa {mb}MB', { mb: FILE_MAX_MB }));
    }
    setError('');
    setFile(f ?? null);
  };
  const submit = () => {
    if (kind === 'su_co' && !req.trim()) return setError(t('Nhập người yêu cầu'));
    if (kind !== 'gop_y' && !contact.trim()) return setError(t('Nhập SĐT hoặc email liên hệ'));
    if (!kind) return;
    setError('');
    create.mutate(
      {
        kind,
        bill: kind === 'su_co' ? bill.trim() : undefined,
        cnee: context?.cnee,
        ct: context?.ct,
        type: kind === 'gop_y' ? undefined : type || 'Khác',
        subject: kind === 'ho_tro' ? subject.trim() : undefined,
        lv: kind === 'su_co' ? lv || 'mid' : undefined,
        desc: desc.trim(),
        req: req.trim(),
        contact: contact.trim(),
        file
      },
      { onSuccess: onClose }
    );
  };

  const typeOptions: readonly string[] = kind === 'ho_tro' ? SUPPORT_TOPICS : TROUBLE_TYPES;
  const suggestions = kind === 'su_co' && type ? TROUBLE_SUGGESTIONS[type as TroubleType] ?? [] : [];

  return (
    <Modal
      open={open}
      title={ctxBill ? 'Báo cáo sự cố đơn hàng' : 'Gửi yêu cầu cho CS Việt An'}
      onClose={onClose}
      footerNote="Yêu cầu được gửi tới bộ phận CS Việt An"
    >
      {kind === 'su_co' && bill.trim() && (ctxBill || done('bill')) && (
        <div className={styles.order}>
          <Icon name="box" size={15} />
          <span>{t('Vận đơn')}</span>
          <strong className="mono">{bill.trim()}</strong>
          {context?.cnee && <span className={styles.orderSub}>· {context.cnee}{context.ct ? ` · ${context.ct}` : ''}</span>}
        </div>
      )}

      <div className={styles.chat}>
        <Bot>{t('Xin chào, CS Việt An sẵn sàng hỗ trợ bạn. Chỉ cần trả lời vài câu hỏi dưới đây.')}</Bot>

        {order.includes('kind') && (
          <>
            <Bot>{t('Bạn cần gửi gì cho Việt An?')}</Bot>
            {done('kind') && kind ? (
              <Me onEdit={() => goTo('kind')}>{t(TROUBLE_KINDS[kind].label)}</Me>
            ) : (
              <div className={styles.kinds}>
                {TROUBLE_KIND_ORDER.map(k => (
                  <button key={k} type="button" className={styles.kind} onClick={() => pickKind(k)}>
                    <span className={cx(styles.kindIcon, styles[`kind_${k}`])}><Icon name={TROUBLE_KINDS[k].icon} size={17} /></span>
                    <span className={styles.kindText}>
                      <strong>{t(TROUBLE_KINDS[k].label)}</strong>
                      <span>{t(TROUBLE_KINDS[k].hint)}</span>
                    </span>
                  </button>
                ))}
              </div>
            )}
          </>
        )}

        {!ctxBill && shows('bill') && (
          <>
            <Bot>{t('Bạn cần hỗ trợ đơn nào? Nhập mã vận đơn (VA Bill).')}</Bot>
            {done('bill') ? (
              <Me onEdit={() => goTo('bill')}><span className="mono">{bill.trim()}</span></Me>
            ) : (
              <div className={styles.inputRow}>
                <input autoFocus className={styles.input} value={bill} onChange={e => setBill(e.target.value)} onKeyDown={e => e.key === 'Enter' && confirmBill()} placeholder="6179215" aria-label={t('Mã vận đơn (VA Bill)')} />
                <Button variant="primary" size="sm" onClick={confirmBill}>{t('Tiếp tục')}</Button>
              </div>
            )}
          </>
        )}

        {shows('type') && (
          <>
            <Bot>{t(kind === 'ho_tro' ? 'Bạn cần hỗ trợ về vấn đề gì?' : 'Đơn đang gặp vấn đề gì?')}</Bot>
            {done('type') ? (
              <Me onEdit={() => goTo('type')}>{t(type)}</Me>
            ) : (
              <div className={styles.chips}>
                {typeOptions.map(x => (
                  <button key={x} type="button" className={styles.chip} onClick={() => { setType(x); next('type'); }}>{t(x)}</button>
                ))}
              </div>
            )}
          </>
        )}

        {shows('desc') && (
          <>
            <Bot>
              {t(kind === 'su_co' ? 'Bạn chọn nhanh các ý dưới đây hoặc gõ thêm chi tiết nhé.'
                : kind === 'ho_tro' ? 'Bạn cho CS biết tiêu đề và nội dung cần hỗ trợ nhé.'
                : 'Bạn muốn góp ý điều gì cho Việt An?')}
            </Bot>
            {done('desc') ? (
              <Me onEdit={() => goTo('desc')}>{kind === 'ho_tro' ? `${subject.trim()}\n${desc.trim()}` : desc.trim()}</Me>
            ) : (
              <div className={styles.answer}>
                {suggestions.length > 0 && (
                  <div className={styles.chips}>
                    {suggestions.map(s => {
                      const on = hasSentence(desc, t(s));
                      return (
                        <button key={s} type="button" aria-pressed={on} className={cx(styles.chip, on && styles.chipOn)} onClick={() => setDesc(d => toggleSentence(d, t(s)))}>
                          {on ? <Icon name="check" size={13} /> : <Icon name="plus" size={13} />} {t(s)}
                        </button>
                      );
                    })}
                  </div>
                )}
                {kind === 'ho_tro' && (
                  <input className={styles.input} value={subject} onChange={e => setSubject(e.target.value)} placeholder={t('Tiêu đề, vd: Không in được nhãn')} aria-label={t('Tiêu đề')} maxLength={200} />
                )}
                <textarea
                  className={styles.textarea}
                  rows={kind === 'su_co' ? 3 : 5}
                  value={desc}
                  onChange={e => setDesc(e.target.value)}
                  placeholder={t(kind === 'su_co' ? 'Điều gì xảy ra, khi nào, bạn mong muốn xử lý thế nào…' : kind === 'ho_tro' ? 'Mô tả chi tiết điều bạn cần hỗ trợ…' : 'Điều bạn thấy chưa tiện, ý tưởng cải thiện…')}
                  aria-label={t('Nội dung')}
                  maxLength={4000}
                />
                <div className={styles.actions}>
                  <Button variant="primary" size="sm" onClick={confirmDesc}>{t('Tiếp tục')}</Button>
                </div>
              </div>
            )}
          </>
        )}

        {shows('lv') && (
          <>
            <Bot>{t('Mức độ cần xử lý?')}</Bot>
            {done('lv') && lv ? (
              <Me onEdit={() => goTo('lv')}>{t(TROUBLE_PRIORITY[lv].label)}</Me>
            ) : (
              <div className={styles.chips}>
                {PRIORITIES.map(p => (
                  <button key={p} type="button" className={cx(styles.chip, styles[`lv_${p}`])} onClick={() => { setLv(p); next('lv'); }}>{t(TROUBLE_PRIORITY[p].label)}</button>
                ))}
              </div>
            )}
          </>
        )}

        {step === 'contact' && (
          <>
            <Bot>
              {t(kind === 'gop_y' ? 'Để lại liên hệ nếu bạn muốn CS phản hồi (không bắt buộc), kèm ảnh nếu có rồi bấm Gửi.' : 'CS sẽ liên hệ ai khi có kết quả? Kiểm tra lại giúp mình, kèm ảnh / chứng từ nếu có rồi bấm Gửi.')}
            </Bot>
            <div className={styles.answer}>
              <div className={styles.contact}>
                {kind === 'su_co' && (
                  <label>
                    <span>{t('Người yêu cầu')}</span>
                    <input className={styles.input} value={req} onChange={e => setReq(e.target.value)} />
                  </label>
                )}
                <label>
                  <span>{t('SĐT / email liên hệ')}</span>
                  <input className={styles.input} value={contact} onChange={e => setContact(e.target.value)} />
                </label>
              </div>
              <label className={styles.file}>
                <Icon name="file" size={15} />
                <span className={styles.fileName}>{file ? `${file.name} · ${(file.size / 1024).toFixed(0)} KB` : t('Đính kèm ảnh / chứng từ (tùy chọn, tối đa {mb}MB — gửi kèm email cho CS)', { mb: FILE_MAX_MB })}</span>
                <input type="file" accept={FILE_ACCEPT} onChange={e => pickFile(e.target.files?.[0])} aria-label={t('Đính kèm ảnh / chứng từ')} />
              </label>
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
