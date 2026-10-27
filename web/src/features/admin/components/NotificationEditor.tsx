import { useEffect, useLayoutEffect, useRef, useState, type ClipboardEvent, type DragEvent, type TextareaHTMLAttributes } from 'react';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Drawer, Icon, Modal, Notice } from '@/shared/ui';
import { editorValue, NoticeContent, notificationImageUrl, toPlainText } from '@/features/notifications';
import { uploadNotificationImage, useNotificationAudience, useSaveNotification, useTestNotificationEmail, type AdminNotification } from '../api';
import {
  copyDraft, draftFrom, emailLocked, emptyDraft, emptyItem, LIMITS, move, recipientsOf, summaryOf, toRequest, validateDraft, type Draft, type DraftItem, type ItemAction
} from '../lib/editor';
import { prepareImage } from '../lib/image';
import { RichEditor, RichToolbar } from './RichEditor';
import styles from './NotificationEditor.module.css';

interface NotificationEditorProps {
  /** null = soạn mới. */
  source: AdminNotification | null;
  /** Nhân bản: soạn mới từ nội dung thông báo này (source = null). */
  copyOf?: AdminNotification | null;
  onClose: () => void;
  onSaved: (message: string) => void;
}

/**
 * Trình soạn thông báo toàn trang kiểu bài viết: giữa là "trang giấy" trông như khách đọc (tiêu đề lớn, các tin nối nhau,
 * ảnh cỡ thật, nút có chữ: Thêm ảnh / Thêm link / Lên / Xuống / Xóa tin / Chèn tin), phải là cài đặt gửi (Gửi tới · Hiển thị · Thời gian · Tóm tắt).
 * Ảnh tải lên ngay khi chọn / kéo thả / dán (Ctrl+V vào tin đang soạn); "Xem như khách" mở khung y như khách thấy.
 */
export function NotificationEditor({ source, copyOf, onClose, onSaved }: NotificationEditorProps) {
  const { t } = useI18n();
  const audience = useNotificationAudience();
  const save = useSaveNotification();
  const [draft, setDraft] = useState<Draft>(() =>
    source ? draftFrom(source, new Date()) : copyOf ? copyDraft(copyOf, new Date()) : emptyDraft(new Date())
  );
  const [focusKey, setFocusKey] = useState(() => draft.items[0]?.key ?? '');
  const [uploading, setUploading] = useState<Record<string, number>>({});
  const [error, setError] = useState<string | null>(null);
  const [preview, setPreview] = useState(false);
  const [dirty, setDirty] = useState(!!copyOf);
  const [testing, setTesting] = useState(false);
  const topRef = useRef<HTMLDivElement>(null);

  const published = !!source && ['live', 'scheduled', 'expired'].includes(source.status);
  const lockedEmail = emailLocked(source);
  const sum = summaryOf(draft);
  const total = recipientsOf(draft, audience.data);
  const busy = Object.values(uploading).some(n => n > 0);

  // Khóa cuộn trang phía sau khi trình soạn mở.
  useEffect(() => {
    const prev = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    return () => void (document.body.style.overflow = prev);
  }, []);

  const update = (patch: Partial<Draft>) => {
    setDraft(d => ({ ...d, ...patch }));
    setDirty(true);
  };
  const updateItem = (key: string, patch: Partial<DraftItem> | ((i: DraftItem) => Partial<DraftItem>)) => {
    setDraft(d => ({ ...d, items: d.items.map(i => (i.key === key ? { ...i, ...(typeof patch === 'function' ? patch(i) : patch) } : i)) }));
    setDirty(true);
  };
  const insertAt = (at: number) => {
    const fresh = emptyItem();
    setDraft(d => ({ ...d, items: [...d.items.slice(0, at), fresh, ...d.items.slice(at)] }));
    setFocusKey(fresh.key);
    setDirty(true);
    requestAnimationFrame(() => document.querySelector<HTMLTextAreaElement>(`[data-item="${fresh.key}"] textarea`)?.focus());
  };
  const moveItem = (from: number, to: number) => update({ items: move(draft.items, from, to) });
  const removeItem = (key: string, index: number) => {
    if (!window.confirm(t('Xóa tin {n}?', { n: index + 1 }))) return;
    update({ items: draft.items.filter(i => i.key !== key) });
  };

  const addFiles = async (key: string, files: File[]) => {
    const it = draft.items.find(i => i.key === key);
    if (!it) return;
    const images = files.filter(f => f.type.startsWith('image/'));
    const room = LIMITS.images - it.images.length - (uploading[key] ?? 0);
    if (images.length > room) setError(t('Mỗi tin tối đa {n} ảnh', { n: LIMITS.images }));
    setFocusKey(key);
    for (const file of images.slice(0, Math.max(0, room))) {
      setUploading(u => ({ ...u, [key]: (u[key] ?? 0) + 1 }));
      try {
        const { blob, name } = await prepareImage(file);
        const saved = await uploadNotificationImage(blob, name);
        updateItem(key, i => ({ images: [...i.images, saved] }));
      } catch (e) {
        setError(e instanceof Error && !('status' in e) ? t(e.message) : getErrorMessage(e));
      } finally {
        setUploading(u => ({ ...u, [key]: Math.max(0, (u[key] ?? 1) - 1) }));
      }
    }
  };

  const onPaste = (e: ClipboardEvent) => {
    const files = [...e.clipboardData.files];
    if (!files.length) return;
    e.preventDefault();
    void addFiles(focusKey || draft.items[0]!.key, files);
  };

  const submit = (publish: boolean) => {
    const problem = busy ? 'Đợi ảnh tải lên xong' : validateDraft(draft, publish, new Date());
    if (problem) {
      setError(problem);
      topRef.current?.scrollIntoView({ behavior: 'smooth' });
      return;
    }
    if (publish && !published) {
      const question = draft.schedule ? 'Hẹn đăng thông báo tới {n} khách?' : draft.sendEmail ? 'Đăng thông báo và gửi email tới {n} khách?' : 'Đăng thông báo tới {n} khách?';
      if (!window.confirm(t(question, { n: total }))) return;
    }
    setError(null);
    save.mutate(
      { id: draft.id, body: toRequest(draft, publish) },
      { onSuccess: r => onSaved(r.message), onError: e => setError(getErrorMessage(e)) }
    );
  };

  // Gửi thử chỉ cần nội dung hợp lệ — bỏ qua đối tượng nhận / giờ đăng.
  const openTest = () => {
    const problem = busy ? 'Đợi ảnh tải lên xong' : validateDraft({ ...draft, audience: 'all', schedule: false, expiresOn: '' }, true, new Date());
    if (problem) {
      setError(problem);
      topRef.current?.scrollIntoView({ behavior: 'smooth' });
      return;
    }
    setError(null);
    setTesting(true);
  };

  const close = () => {
    if (dirty && !window.confirm(t('Thoát trình soạn? Nội dung chưa lưu sẽ mất.'))) return;
    onClose();
  };

  const publishLabel = draft.schedule ? 'Hẹn đăng' : published ? 'Lưu và đăng' : 'Đăng thông báo';
  const meta = draft.schedule
    ? t('Việt An Express · đăng lúc {at}', { at: draft.publishAt.replace('T', ' ') })
    : t('Việt An Express · đăng ngay khi bấm Đăng');
  const previewParts = toRequest(draft, true).items.map(i => ({ ...i, images: i.images }));

  return (
    <div className={styles.editor} role="dialog" aria-modal="true" aria-label={t(source ? 'Sửa thông báo' : 'Soạn thông báo')} onPaste={onPaste}>
      <header className={styles.top}>
        <button type="button" className={styles.back} onClick={close}>
          <Icon name="chevronDown" size={16} className={styles.backIcon} /> {t('Thông báo')}
        </button>
        <span className={styles.state}>{t(source ? 'Đang sửa thông báo' : copyOf ? 'Bản sao thông báo' : 'Thông báo mới')}</span>
        <span className={styles.spacer} />
        <Button variant="ghost" onClick={() => setPreview(true)}>
          <Icon name="eye" size={15} /> {t('Xem như khách')}
        </Button>
        <Button variant="ghost" onClick={openTest}>
          <Icon name="mail" size={15} /> {t('Gửi thử email')}
        </Button>
        {!published && (
          <Button onClick={() => submit(false)} disabled={save.isPending}>
            {t('Lưu nháp')}
          </Button>
        )}
        <Button variant="primary" onClick={() => submit(true)} disabled={save.isPending}>
          <Icon name="send" size={15} /> {t(save.isPending ? 'Đang lưu…' : publishLabel)}
        </Button>
      </header>

      <div className={styles.scroll}>
        <div className={styles.grid}>
          <div className={styles.column}>
          <RichToolbar className={styles.toolbar} />
          <div className={styles.paper} ref={topRef}>
            {error && <Notice tone="danger">{t(error)}</Notice>}
            {(draft.pinned || draft.important) && (
              <div className={styles.chips}>
                {draft.pinned && <span className={cx(styles.chip, styles.chipPin)}>{t('Ghim')}</span>}
                {draft.important && <span className={cx(styles.chip, styles.chipImp)}>{t('Quan trọng')}</span>}
              </div>
            )}
            <Grow
              className={styles.title}
              value={draft.title}
              maxLength={LIMITS.title}
              placeholder={t('Tiêu đề thông báo')}
              onChange={e => update({ title: e.target.value })}
              aria-label={t('Tiêu đề thông báo')}
            />
            <p className={styles.meta}>{meta}</p>

            {draft.items.map((it, i) => (
              <div key={it.key}>
                {i > 0 && (
                  <div className={styles.insert}>
                    <button type="button" onClick={() => insertAt(i)}>
                      <Icon name="plus" size={13} /> {t('Chèn tin ở đây')}
                    </button>
                  </div>
                )}
                <ItemEditor
                  item={it}
                  index={i}
                  count={draft.items.length}
                  focused={focusKey === it.key}
                  uploading={uploading[it.key] ?? 0}
                  onFocus={() => setFocusKey(it.key)}
                  onChange={patch => updateItem(it.key, patch)}
                  onMove={to => moveItem(i, to)}
                  onRemove={() => removeItem(it.key, i)}
                  onFiles={files => void addFiles(it.key, files)}
                />
              </div>
            ))}
            {draft.items.length < LIMITS.items && (
              <button type="button" className={styles.addItem} onClick={() => insertAt(draft.items.length)}>
                <Icon name="plus" size={16} /> {t('Thêm tin')}
              </button>
            )}
          </div>
          </div>

          <aside className={styles.settings}>
            <section className={styles.panel}>
              <h4>{t('Gửi tới')}</h4>
              <Radio checked={draft.audience === 'all'} onChange={() => update({ audience: 'all' })} title="Mọi khách"
                hint={t('Tất cả {n} khách có tài khoản portal', { n: audience.data?.total ?? 0 })} />
              <Radio checked={draft.audience === 'type'} onChange={() => update({ audience: 'type' })} title="Theo loại khách" hint={t('Chọn một hoặc nhiều loại')} />
              {draft.audience === 'type' && (
                <div className={styles.types}>
                  {!audience.data?.types.length && <p className={styles.hint}>{t('Chưa có khách nào được phân loại (CustomerTypeID).')}</p>}
                  {audience.data?.types.map(type => {
                    const on = draft.customerTypes.includes(type.id);
                    return (
                      <label key={type.id} className={cx(styles.type, on && styles.typeOn)}>
                        <input
                          type="checkbox"
                          checked={on}
                          onChange={e => update({ customerTypes: e.target.checked ? [...draft.customerTypes, type.id] : draft.customerTypes.filter(x => x !== type.id) })}
                        />
                        {t(type.name)} <span>{type.count}</span>
                      </label>
                    );
                  })}
                </div>
              )}
              <Radio checked={draft.audience === 'pick'} onChange={() => update({ audience: 'pick' })} title="Chọn khách" hint={t('Nhập mã từng khách')} />
              {draft.audience === 'pick' && (
                <textarea
                  className={styles.codes}
                  rows={3}
                  value={draft.customerCodes}
                  onChange={e => update({ customerCodes: e.target.value })}
                  placeholder={t('Mã khách, cách nhau dấu phẩy hoặc xuống dòng')}
                  aria-label={t('Mã khách')}
                />
              )}
            </section>

            <section className={styles.panel}>
              <h4>{t('Hiển thị')}</h4>
              <Toggle checked={draft.important} onChange={v => update({ important: v })} title="Quan trọng" hint="Hiện popup khi khách mở portal" />
              <Toggle checked={draft.pinned} onChange={v => update({ pinned: v })} title="Ghim lên đầu" hint="Nằm trên cùng danh sách của khách" />
              <Toggle
                checked={draft.sendEmail}
                onChange={v => update({ sendEmail: v })}
                disabled={lockedEmail}
                title="Gửi kèm email"
                hint={lockedEmail ? 'Email đã gửi, không gửi lại' : 'Gửi tới email của khách nhận (tối đa 450 địa chỉ)'}
              />
            </section>

            <section className={styles.panel}>
              <h4>{t('Thời gian')}</h4>
              <Toggle checked={draft.schedule} onChange={v => update({ schedule: v })} title="Hẹn giờ đăng" hint="Tự đăng vào lúc chọn" />
              {draft.schedule && (
                <input
                  type="datetime-local"
                  className={styles.input}
                  value={draft.publishAt}
                  onChange={e => update({ publishAt: e.target.value })}
                  aria-label={t('Giờ đăng')}
                />
              )}
              <label className={styles.field}>
                {t('Hiện đến ngày')} <span>{t('· không bắt buộc')}</span>
                <input type="date" className={styles.input} value={draft.expiresOn} onChange={e => update({ expiresOn: e.target.value })} />
              </label>
            </section>

            <section className={cx(styles.panel, styles.summary)}>
              <div><span>{t('Nội dung')}</span><b>{t('{a} tin · {b} ảnh · {c} nút xem chi tiết', { a: sum.items, b: sum.images, c: sum.links + sum.details })}</b></div>
              <div><span>{t('Gửi tới')}</span><b>{t('{n} khách', { n: total })}</b></div>
              <div><span>{t('Đăng')}</span><b>{draft.schedule ? draft.publishAt.replace('T', ' ') : t(published ? 'Giữ giờ đăng' : 'Ngay')}</b></div>
              {draft.expiresOn && <div><span>{t('Hiện đến')}</span><b>{draft.expiresOn.split('-').reverse().join('/')}</b></div>}
              {draft.sendEmail && <div><span>{t('Email')}</span><b>{t(lockedEmail ? 'Đã gửi' : 'Có gửi')}</b></div>}
            </section>
          </aside>
        </div>
      </div>

      <Drawer open={preview} title="Xem trước — khách thấy như sau" onClose={() => setPreview(false)}>
        <NoticeContent
          title={draft.title.trim() || t('Tiêu đề thông báo')}
          meta={meta}
          imp={draft.important}
          pin={draft.pinned}
          parts={previewParts.length ? previewParts : [{ title: null, body: t('Nội dung thông báo sẽ hiện ở đây.'), images: [], link: null }]}
        />
      </Drawer>

      {testing && <TestEmailDialog draft={draft} onClose={() => setTesting(false)} />}
    </div>
  );
}

const TEST_EMAIL_KEY = 'va.admin.testEmail';
const lastTestEmail = () => {
  try {
    return window.localStorage.getItem(TEST_EMAIL_KEY) ?? '';
  } catch {
    return '';
  }
};

/** Gửi thử email của nội dung đang soạn tới vài địa chỉ (nhớ địa chỉ lần trước trên máy này) — không lưu, không gửi khách. */
function TestEmailDialog({ draft, onClose }: { draft: Draft; onClose: () => void }) {
  const { t } = useI18n();
  const send = useTestNotificationEmail();
  const [to, setTo] = useState(lastTestEmail);
  const [result, setResult] = useState<{ ok: boolean; text: string } | null>(null);

  const submit = () => {
    const { title, items, important } = toRequest(draft, true);
    setResult(null);
    send.mutate(
      { to, title, items, important },
      {
        onSuccess: r => {
          try {
            window.localStorage.setItem(TEST_EMAIL_KEY, to.trim());
          } catch {
            /* storage bị chặn: lần sau nhập lại */
          }
          setResult({ ok: true, text: t('Đã gửi email thử tới {n} địa chỉ — kiểm tra hộp thư (cả mục Spam).', { n: r.data }) });
        },
        onError: e => setResult({ ok: false, text: getErrorMessage(e) })
      }
    );
  };

  return (
    <Modal
      open
      title={t('Gửi thử email')}
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>{t('Đóng')}</Button>
          <Button variant="primary" onClick={submit} disabled={!to.trim() || send.isPending}>
            <Icon name="send" size={15} /> {t(send.isPending ? 'Đang gửi…' : 'Gửi thử')}
          </Button>
        </>
      }
    >
      <div className={styles.testEmail}>
        <p className={styles.hint}>
          {t('Gửi nội dung đang soạn tới email của bạn để xem trước trên hộp thư. Thông báo không được lưu và không gửi cho khách.')}
        </p>
        <label className={styles.field}>
          {t('Email nhận thử')} <span>{t('· tối đa 5 địa chỉ, cách nhau dấu phẩy')}</span>
          <input
            className={styles.input}
            type="text"
            inputMode="email"
            value={to}
            onChange={e => setTo(e.target.value)}
            onKeyDown={e => { if (e.key === 'Enter' && to.trim() && !send.isPending) submit(); }}
            placeholder="ten@vietanexpress.com.vn"
            autoFocus
          />
        </label>
        {result && <Notice tone={result.ok ? 'success' : 'danger'}>{result.text}</Notice>}
      </div>
    </Modal>
  );
}

interface ItemEditorProps {
  item: DraftItem;
  index: number;
  count: number;
  focused: boolean;
  uploading: number;
  onFocus: () => void;
  onChange: (patch: Partial<DraftItem>) => void;
  onMove: (to: number) => void;
  onRemove: () => void;
  onFiles: (files: File[]) => void;
}

/** 1 tin trên trang giấy: tiêu đề, nội dung có định dạng, ảnh cỡ thật (đổi chỗ / bỏ), nút "Xem chi tiết" (link / nội dung chi tiết); kéo thả ảnh vào tin. */
function ItemEditor({ item: it, index: i, count, focused, uploading, onFocus, onChange, onMove, onRemove, onFiles }: ItemEditorProps) {
  const { t } = useI18n();
  const [over, setOver] = useState(false);
  const [editingDetail, setEditingDetail] = useState(false);
  const many = count > 1;
  const hasFiles = (e: DragEvent) => [...e.dataTransfer.types].includes('Files');

  return (
    <section
      data-item={it.key}
      className={cx(styles.part, focused && styles.partFocus, over && styles.partOver)}
      onFocus={onFocus}
      onMouseDown={onFocus}
      onDragOver={e => { if (hasFiles(e)) { e.preventDefault(); setOver(true); } }}
      onDragLeave={e => { if (!e.currentTarget.contains(e.relatedTarget as Node | null)) setOver(false); }}
      onDrop={e => { if (!hasFiles(e)) return; e.preventDefault(); setOver(false); onFiles([...e.dataTransfer.files]); }}
    >
      <div className={styles.partHead}>
        <span className={styles.partNo}>{many ? t('Tin {i} / {n}', { i: i + 1, n: count }) : t('Nội dung')}</span>
        <span className={styles.tools}>
          <button type="button" className={styles.tool} onClick={() => onMove(i - 1)} disabled={i === 0}>↑ {t('Lên')}</button>
          <button type="button" className={styles.tool} onClick={() => onMove(i + 1)} disabled={i === count - 1}>↓ {t('Xuống')}</button>
          <button type="button" className={cx(styles.tool, styles.danger)} onClick={onRemove} disabled={!many}>
            <Icon name="trash" size={13} /> {t('Xóa tin')}
          </button>
        </span>
      </div>
      <Grow
        className={styles.partTitle}
        value={it.title}
        maxLength={LIMITS.title}
        placeholder={t(many ? 'Tiêu đề tin' : 'Tiêu đề tin (không bắt buộc)')}
        onChange={e => onChange({ title: e.target.value })}
        aria-label={t('Tiêu đề tin')}
      />
      <RichEditor
        initial={it.body}
        onChange={body => onChange({ body })}
        className={styles.partBody}
        placeholder={t('Viết nội dung tin…')}
        ariaLabel={t('Nội dung tin')}
      />

      {(it.images.length > 0 || uploading > 0) && (
        <div className={cx(styles.gallery, it.images.length + uploading > 1 && styles.galleryMany)}>
          {it.images.map((name, k) => (
            <div key={name + k} className={styles.image}>
              <img src={notificationImageUrl(name)} alt="" />
              <span className={styles.imageNo}>{t('Ảnh {n}', { n: k + 1 })}</span>
              <span className={styles.imageTools}>
                <button type="button" onClick={() => onChange({ images: move(it.images, k, k - 1) })} disabled={k === 0} aria-label={t('Đưa ảnh lên trước')}>←</button>
                <button type="button" onClick={() => onChange({ images: move(it.images, k, k + 1) })} disabled={k === it.images.length - 1} aria-label={t('Đưa ảnh ra sau')}>→</button>
                <button type="button" onClick={() => onChange({ images: it.images.filter((_, x) => x !== k) })}>
                  <Icon name="close" size={12} /> {t('Bỏ ảnh')}
                </button>
              </span>
            </div>
          ))}
          {Array.from({ length: uploading }, (_, k) => (
            <div key={`up-${k}`} className={cx(styles.image, styles.uploading)}>{t('Đang tải ảnh…')}</div>
          ))}
        </div>
      )}

      <div className={styles.cta}>
        <span className={styles.ctaLabel}>{t('Nút "Xem chi tiết"')}</span>
        <span className={styles.ctaOptions} role="radiogroup" aria-label={t('Nút "Xem chi tiết"')}>
          {ACTIONS.map(a => (
            <button key={a.key} type="button" role="radio" aria-checked={it.action === a.key} className={cx(styles.ctaOption, it.action === a.key && styles.ctaOn)}
              onClick={() => { onChange({ action: a.key }); if (a.key === 'detail' && !toPlainText(editorValue(it.detail))) setEditingDetail(true); }}>
              {t(a.label)}
            </button>
          ))}
        </span>
      </div>

      {it.action === 'link' && (
        <div className={styles.link}>
          <Icon name="link" size={15} />
          <span className={styles.linkLabel}>{t('Link của tin')}</span>
          <input
            value={it.link}
            maxLength={LIMITS.link}
            placeholder="https://"
            onChange={e => onChange({ link: e.target.value })}
            aria-label={t('Link của tin')}
            autoFocus={!it.link}
          />
        </div>
      )}

      {it.action === 'detail' && (
        <div className={styles.detailBox}>
          <span className={styles.detailText}>
            {toPlainText(editorValue(it.detail)) || t('Chưa có nội dung chi tiết')}
          </span>
          <button type="button" className={styles.action} onClick={() => setEditingDetail(true)}>
            <Icon name="edit" size={14} /> {t(toPlainText(editorValue(it.detail)) ? 'Sửa nội dung chi tiết' : 'Soạn nội dung chi tiết')}
          </button>
        </div>
      )}

      <div className={styles.actions}>
        {it.images.length + uploading < LIMITS.images && (
          <label className={styles.action}>
            <Icon name="image" size={15} /> {t('Thêm ảnh')}
            <input
              type="file"
              accept="image/jpeg,image/png,image/webp"
              multiple
              hidden
              onChange={e => { onFiles([...(e.target.files ?? [])]); e.target.value = ''; }}
            />
          </label>
        )}
        <span className={styles.hint}>
          {it.images.length > 0 && t('{n}/5 ảnh · ', { n: it.images.length })}
          {t('Kéo thả hoặc dán ảnh (Ctrl+V) vào tin này')}
        </span>
      </div>

      {editingDetail && (
        <DetailEditor
          title={it.title.trim() || t('Tin {n}', { n: i + 1 })}
          initial={it.detail}
          onCancel={() => { setEditingDetail(false); if (!toPlainText(editorValue(it.detail))) onChange({ action: 'none' }); }}
          onSave={detail => { onChange({ detail, action: 'detail' }); setEditingDetail(false); }}
        />
      )}
    </section>
  );
}

const ACTIONS: ReadonlyArray<{ key: ItemAction; label: string }> = [
  { key: 'none', label: 'Không có' },
  { key: 'link', label: 'Mở link' },
  { key: 'detail', label: 'Mở nội dung chi tiết' }
];

/** Popup soạn nội dung chi tiết của 1 tin — khách bấm "Xem chi tiết" sẽ mở nội dung này. */
function DetailEditor({ title, initial, onCancel, onSave }: { title: string; initial: string; onCancel: () => void; onSave: (html: string) => void }) {
  const { t } = useI18n();
  const [html, setHtml] = useState(initial);
  const text = toPlainText(editorValue(html));
  return (
    <Modal
      open
      size="lg"
      title={t('Nội dung chi tiết — {title}', { title })}
      onClose={onCancel}
      footerNote={t('{n} / {max} ký tự', { n: text.length, max: LIMITS.detail })}
      footer={
        <>
          <Button variant="ghost" onClick={onCancel}>{t('Hủy')}</Button>
          <Button variant="primary" onClick={() => onSave(html)} disabled={!text || text.length > LIMITS.detail}>
            <Icon name="check" size={15} /> {t('Lưu nội dung chi tiết')}
          </Button>
        </>
      }
    >
      <div className={styles.detailEditor}>
        <RichToolbar />
        <RichEditor
          initial={initial}
          onChange={setHtml}
          className={styles.detailArea}
          placeholder={t('Viết nội dung chi tiết khách xem khi bấm "Xem chi tiết"…')}
          ariaLabel={t('Nội dung chi tiết')}
          autoFocus
        />
      </div>
    </Modal>
  );
}

/** Ô chữ không viền tự cao theo nội dung. */
function Grow({ className, value, ...rest }: TextareaHTMLAttributes<HTMLTextAreaElement> & { value: string }) {
  const ref = useRef<HTMLTextAreaElement>(null);
  useLayoutEffect(() => {
    const el = ref.current;
    if (!el) return;
    el.style.height = 'auto';
    el.style.height = `${el.scrollHeight}px`;
  }, [value]);
  return <textarea ref={ref} rows={1} className={cx(styles.bare, className)} value={value} {...rest} />;
}

function Radio({ checked, onChange, title, hint }: { checked: boolean; onChange: () => void; title: string; hint: string }) {
  const { t } = useI18n();
  return (
    <label className={cx(styles.radio, checked && styles.radioOn)}>
      <input type="radio" checked={checked} onChange={onChange} />
      <span>
        <b>{t(title)}</b>
        <small>{hint}</small>
      </span>
    </label>
  );
}

function Toggle({ checked, onChange, title, hint, disabled }: { checked: boolean; onChange: (v: boolean) => void; title: string; hint: string; disabled?: boolean }) {
  const { t } = useI18n();
  return (
    <label className={cx(styles.toggleRow, disabled && styles.disabled)}>
      <span>
        <b>{t(title)}</b>
        <small>{t(hint)}</small>
      </span>
      <span className={styles.toggle}>
        <input type="checkbox" checked={checked} disabled={disabled} onChange={e => onChange(e.target.checked)} />
        <span />
      </span>
    </label>
  );
}
