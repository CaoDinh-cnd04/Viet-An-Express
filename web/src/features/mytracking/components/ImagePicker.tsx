import { useRef, useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { Button, Icon, TextField, useToast } from '@/shared/ui';
import { isWebUrl } from '../schema';
import { readImage, validateImageUrl } from '../lib/images';
import styles from '../pages/MyTrackingPage.module.css';

interface Props {
  onAdd: (src: string) => void;
  disabled?: boolean;
  onBusy: (busy: boolean) => void;
  label?: string;
  /** false = chỉ tải ảnh từ máy (logo công ty), không cho nhập URL. */
  allowUrl?: boolean;
}

export function ImagePicker({ onAdd, disabled, onBusy, label, allowUrl = true }: Props) {
  const { t } = useI18n();
  const toast = useToast();
  const fileInput = useRef<HTMLInputElement>(null);
  const [url, setUrl] = useState('');
  const [loading, setLoading] = useState(false);
  const addFromUrl = async () => {
    if (disabled || loading || !url.trim()) return;
    const src = url.trim();
    if (!isWebUrl(src)) { toast.show(t('Link phải bắt đầu bằng http:// hoặc https://'), 'error'); return; }
    setLoading(true); onBusy(true);
    try { onAdd(await validateImageUrl(src)); setUrl(''); }
    catch (error) { toast.show(t(error instanceof Error ? error.message : 'Không đọc được hình ảnh'), 'error'); }
    finally { setLoading(false); onBusy(false); }
  };
  return <div>
    <div className={allowUrl ? styles.source : undefined}>
      <Button variant="primary" disabled={disabled || loading} onClick={() => fileInput.current?.click()}>
        <Icon name="upload" size={14} /> {t(label === 'Logo' ? (loading ? 'Đang xử lý ảnh…' : 'Tải logo lên') : 'Thêm ảnh mới')}
      </Button>
      {allowUrl && <>
      <TextField label={label === 'Logo' ? 'URL logo' : 'Nhập URL ảnh từ internet'} placeholder="https://" value={url} disabled={disabled || loading} onChange={e => setUrl(e.target.value)} onKeyDown={e => { if (e.key === 'Enter') { e.preventDefault(); void addFromUrl(); } }} />
      <Button disabled={disabled || loading || !url.trim()} onClick={addFromUrl}>{t(loading ? 'Đang xử lý ảnh…' : 'Thêm từ URL')}</Button>
      </>}
    </div>
    <input ref={fileInput} aria-label={t(label === 'Logo' ? 'Tải logo lên' : 'Tải ảnh lên')} type="file" accept="image/jpeg,image/png,image/webp" hidden disabled={disabled || loading} onChange={async e => {
      const file = e.currentTarget.files?.[0]; e.currentTarget.value = ''; if (!file) return;
      setLoading(true); onBusy(true);
      try { onAdd(await readImage(file)); }
      catch (error) { toast.show(t(error instanceof Error ? error.message : 'Không đọc được hình ảnh'), 'error'); }
      finally { setLoading(false); onBusy(false); }
    }} />
    <p className={styles.hint}>{t('JPG, JPEG, PNG, WebP · tối đa 200 KB')}</p>
  </div>;
}
