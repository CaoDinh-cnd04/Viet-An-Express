import { useState, type DragEvent } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Icon, Modal } from '@/shared/ui';
import { editorValue, notificationImageUrl, toPlainText } from '@/features/notifications';
import { LIMITS, move, type DraftItem, type ItemAction } from '../lib/editor';
import { GrowTextarea } from './GrowTextarea';
import { RichEditor, RichToolbar } from './RichEditor';
import styles from './NotificationEditor.module.css';

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
export function NotificationItemEditor({ item: it, index: i, count, focused, uploading, onFocus, onChange, onMove, onRemove, onFiles }: ItemEditorProps) {
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
      <GrowTextarea
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
