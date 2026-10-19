import { useRef, useState, type FormEventHandler } from 'react';
import { useFieldArray, type UseFormReturn } from 'react-hook-form';
import { useI18n } from '@/shared/i18n';
import { uuid } from '@/shared/lib/uuid';
import { Button, Icon, TextAreaField, TextField } from '@/shared/ui';
import type { StorageMode } from '../hooks/useMyTrackingConfig';
import { MAX_AD_IMAGES, type MyTrackingConfig } from '../schema';
import { ConfigImage } from './ConfigImage';
import { ImagePicker } from './ImagePicker';
import styles from '../pages/MyTrackingPage.module.css';

interface Props {
  form: UseFormReturn<MyTrackingConfig>;
  onSave: FormEventHandler<HTMLFormElement>;
  onRestore: () => void;
  disabled: boolean;
  /** `server`: lưu + xuất bản trên server; `local`: chỉ lưu bản thử nghiệm trên trình duyệt. */
  mode: StorageMode;
  published: boolean;
  onPublish: (published: boolean) => void;
}

export function ConfigForm({ form, onSave, onRestore, disabled, mode, published, onPublish }: Props) {
  const { t } = useI18n();
  const { register, control, setValue, getValues, formState: { errors, isDirty } } = form;
  const { fields, append, remove, move } = useFieldArray({ control, name: 'images', keyName: 'fieldKey' });
  const dragId = useRef<string | undefined>(undefined);
  const [uploads, setUploads] = useState(0);
  const config = form.watch();
  const busy = disabled || uploads > 0;
  const onBusy = (loading: boolean) => setUploads(count => count + (loading ? 1 : -1));
  const setImage = (path: 'background' | 'brand.logo', src: string) => setValue(path, src, { shouldDirty: true, shouldValidate: true });

  return <form className={styles.editor} onSubmit={onSave} noValidate aria-busy={busy}>
    <fieldset className={styles.formBody} disabled={busy}>
      <section className={styles.group}>
        <h2>{t('Thông tin thương hiệu')}</h2>
        <div className={styles.brandEditor}>
        <div className={styles.logoEditor}>
          {config.brand.logo
            ? <ConfigImage src={config.brand.logo} alt="Logo" className={styles.logoThumbnail} />
            : <span className={styles.logoPlaceholder}><Icon name="image" size={22} /><span>Logo</span></span>}
          <Button variant="danger" disabled={!config.brand.logo} onClick={() => setImage('brand.logo', '')}>{t('Xóa logo')}</Button>
        </div>
        <div className={styles.brandFields}>
          <TextField label="Tên công ty" placeholder="Tên công ty của bạn" maxLength={150} error={errors.brand?.companyName?.message} {...register('brand.companyName')} />
          <TextField label="Số điện thoại" type="tel" placeholder="Số điện thoại liên hệ" maxLength={50} error={errors.brand?.phone?.message} {...register('brand.phone')} />
        <TextField label="Địa chỉ" placeholder="Địa chỉ công ty" maxLength={500} error={errors.brand?.address?.message} {...register('brand.address')} />
        </div>
        </div>
        <div className={styles.logoSource}><ImagePicker label="Logo" onAdd={src => setImage('brand.logo', src)} onBusy={onBusy} /></div>
      </section>
      <section className={styles.group}>
        <h2>{t('Nội dung hiển thị chính')}</h2>
        <TextField label="Tiêu đề" required maxLength={120} error={errors.title?.message} {...register('title')} />
        <TextAreaField label="Mô tả" rows={4} maxLength={2000} error={errors.description?.message} {...register('description')} />
      </section>
      <section className={styles.group}>
        <div className={styles.groupHeading}><h2>{t('Hình ảnh quảng cáo')}</h2><span>{t('Số lượng: {n}/5', { n: fields.length })}</span></div>
        <p className={styles.hint}>{t('Nhập link website cho từng ảnh. Khách bấm vào ảnh hoặc nút sẽ mở website trong tab mới. Tối đa 5 ảnh.')}</p>
        <ImagePicker onAdd={src => {
          if (getValues('images').length < MAX_AD_IMAGES) append({ id: uuid(), src, linkUrl: '', title: '', buttonText: 'Xem thêm' });
        }} disabled={fields.length >= MAX_AD_IMAGES} onBusy={onBusy} />
        <p className={styles.hint}>{t('Kéo tay cầm để đổi thứ tự ảnh hoặc dùng nút lên / xuống.')}</p>
        {fields.length === 0 && <div className={styles.emptyImages}><Icon name="image" size={24} /><span>{t('Chưa có ảnh quảng cáo. Thêm ảnh để xem trước.')}</span></div>}
        {fields.map((field, index) => <div className={styles.imageRow} key={field.fieldKey} onDragOver={e => e.preventDefault()} onDrop={e => {
          e.preventDefault();
          const from = fields.findIndex(item => item.id === dragId.current);
          if (from >= 0 && from !== index && !busy) move(from, index);
          dragId.current = undefined;
        }}>
          <button type="button" className={styles.dragHandle} draggable={!busy} aria-label={t('Di chuyển ảnh {n}', { n: index + 1 })}
            onDragStart={e => { dragId.current = field.id; e.dataTransfer.setData('text/plain', field.id); e.dataTransfer.effectAllowed = 'move'; }}
            onDragEnd={() => { dragId.current = undefined; }}><Icon name="menu" size={16} /></button>
          <ConfigImage src={config.images[index]?.src ?? field.src} alt={t('Ảnh quảng cáo {n}', { n: index + 1 })} className={styles.thumbnail} />
          <div className={styles.imageFields}>
            <p className={styles.imageName}>{t('Ảnh quảng cáo {n}', { n: index + 1 })}</p>
            <div className={styles.captionFields}>
              <TextField label="Dòng tiêu đề trên ảnh" maxLength={120} error={errors.images?.[index]?.title?.message} {...register(`images.${index}.title`)} />
              <TextField label="Chữ trên nút" maxLength={40} placeholder="Xem thêm" error={errors.images?.[index]?.buttonText?.message} {...register(`images.${index}.buttonText`)} />
            </div>
            <TextField label={t('Link website của ảnh {n}', { n: index + 1 })} type="url" placeholder="https://example.com/dich-vu" error={errors.images?.[index]?.linkUrl?.message} {...register(`images.${index}.linkUrl`)} />
            <p className={styles.hint}>{t('Nút dùng chữ bạn nhập ở trên. Nhập link website hợp lệ để kích hoạt nút; để trống chữ trên nút nếu muốn ẩn nút.')}</p>
          </div>
          <div className={styles.reorder}>
            <Button size="sm" disabled={index === 0} aria-label={t('Đưa ảnh lên')} onClick={() => move(index, index - 1)}>{t('Lên')}</Button>
            <Button size="sm" disabled={index === fields.length - 1} aria-label={t('Đưa ảnh xuống')} onClick={() => move(index, index + 1)}>{t('Xuống')}</Button>
            <Button size="sm" variant="danger" aria-label={t('Xóa ảnh {n}', { n: index + 1 })} onClick={() => remove(index)}>{t('Xóa')}</Button>
          </div>
        </div>)}
      </section>
      <section className={styles.group}>
        <h2>{t('Hình ảnh nền trang')}</h2>
        <ImagePicker onAdd={src => setImage('background', src)} onBusy={onBusy} />
        {config.background
          ? <ConfigImage src={config.background} alt={t('Hình ảnh nền')} className={styles.backgroundPreview} />
          : <div className={styles.backgroundPlaceholder}><Icon name="image" size={28} /><span>{t('Xem trước ảnh nền')}</span></div>}
        <div className={styles.removeBackground}><Button variant="danger" disabled={!config.background} onClick={() => setImage('background', '')}>{t('Xóa ảnh nền')}</Button></div>
      </section>
      <section className={styles.group}>
        <h2>{t('Liên hệ và mạng xã hội')}</h2>
        <p className={styles.hint}>{t('Ô nào để trống thì nút tương ứng sẽ ẩn trên trang tracking.')}</p>
        <div className={styles.twoColumns}>
          <TextField label="Zalo" placeholder="Link hoặc số Zalo" error={errors.brand?.zalo?.message} {...register('brand.zalo')} />
          <TextField label="WhatsApp" placeholder="Số WhatsApp (kèm mã quốc gia)" error={errors.brand?.whatsapp?.message} {...register('brand.whatsapp')} />
        </div>
        <div className={styles.socialForm}>{(['facebook', 'instagram', 'x'] as const).map(key => <TextField key={key} label={{ facebook: 'Facebook', instagram: 'Instagram', x: 'X' }[key]} placeholder="https://" error={errors.brand?.[key]?.message} {...register(`brand.${key}`)} />)}</div>
      </section>
      <div className={styles.actions}>
        {mode === 'local'
          ? <Button type="submit" variant="primary">{t('Lưu bản thử nghiệm')}</Button>
          : <>
            <Button type="submit" variant={published ? 'primary' : undefined}>{t('Lưu')}</Button>
            {!published && <Button variant="primary" onClick={() => onPublish(true)}>{t('Lưu & xuất bản')}</Button>}
          </>}
        <Button onClick={onRestore}>{t('Khôi phục mặc định')}</Button>
        <span className={styles.saveStatus} role="status">{t(isDirty ? 'Có thay đổi chưa lưu' : 'Không có thay đổi chưa lưu')}</span>
        {mode === 'local'
          ? <Button disabled>{t('Xuất bản (sắp ra mắt)')}</Button>
          : published && <Button variant="danger" onClick={() => onPublish(false)}>{t('Ngừng xuất bản')}</Button>}
      </div>
    </fieldset>
  </form>;
}
