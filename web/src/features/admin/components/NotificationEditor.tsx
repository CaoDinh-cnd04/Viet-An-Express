import { useEffect, useRef, useState, type ClipboardEvent } from 'react';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Drawer, Icon, Notice } from '@/shared/ui';
import { NoticeContent } from '@/features/notifications';
import { uploadNotificationImage, useNotificationAudience, useSaveNotification, type AdminNotification } from '../api';
import {
  copyDraft, draftFrom, emailLocked, emptyDraft, emptyItem, LIMITS, move, recipientsOf, toRequest, validateDraft, type Draft, type DraftItem
} from '../lib/editor';
import { prepareImage } from '../lib/image';
import { GrowTextarea } from './GrowTextarea';
import { NotificationItemEditor } from './NotificationItemEditor';
import { NotificationSettings } from './NotificationSettings';
import { RichToolbar } from './RichEditor';
import { TestEmailDialog } from './TestEmailDialog';
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
            <GrowTextarea
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
                <NotificationItemEditor
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

          <NotificationSettings
            draft={draft}
            update={update}
            audience={audience.data}
            total={total}
            published={published}
            lockedEmail={lockedEmail}
          />
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
