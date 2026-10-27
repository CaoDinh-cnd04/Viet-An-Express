import { useRef, useState, type FormEventHandler } from 'react';
import { useFieldArray, type UseFormReturn } from 'react-hook-form';
import { useI18n } from '@/shared/i18n';
import { uuid } from '@/shared/lib/uuid';
import { Button, Icon, TextAreaField, TextField } from '@/shared/ui';
import type { StorageMode } from '../hooks/useMyTrackingConfig';
import { CONTACT_TYPES, isHexColor, MAX_CHANNELS, SOCIAL_TYPES } from '../lib/channels';
import { MAX_AD_IMAGES, type MyTrackingConfig } from '../schema';
import { ChannelListEditor } from './ChannelListEditor';
import { ConfigImage } from './ConfigImage';
import { ImagePicker } from './ImagePicker';
import styles from '../pages/MyTrackingPage.module.css';

interface Props {
  form: UseFormReturn<MyTrackingConfig>;
  onSave: FormEventHandler<HTMLFormElement>;
  onRestore: () => void;
  disabled: boolean;
  /** `server`: lưu trên server (trang hiện công khai ngay); `local`: chỉ lưu bản thử nghiệm trên trình duyệt. */
  mode: StorageMode;
}

export function ConfigForm({ form, onSave, onRestore, disabled, mode }: Props) {
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
          <TextField label="Tên công ty" readOnly hint="Lấy theo hồ sơ khách hàng tại Việt An, không sửa ở đây. Cần đổi tên vui lòng gửi yêu cầu hỗ trợ."
            error={errors.brand?.companyName?.message} {...register('brand.companyName')} />
          <TextField label="Số điện thoại" type="tel" placeholder="Số điện thoại liên hệ" maxLength={50} error={errors.brand?.phone?.message} {...register('brand.phone')} />
        <TextField label="Địa chỉ hiển thị trên trang tracking" placeholder="Địa chỉ công ty" maxLength={250} error={errors.brand?.address?.message} {...register('brand.address')} />
        <TextField label="Website trang home" type="url" placeholder="https://tencongty.vn" hint="Nút Home cạnh nút Track và logo trên trang tracking sẽ mở website này"
          error={errors.brand?.website?.message} {...register('brand.website')} />
        </div>
        </div>
        <div className={styles.logoSource}><ImagePicker label="Logo" allowUrl={false} onAdd={src => setImage('brand.logo', src)} onBusy={onBusy} /></div>
      </section>
      <section className={styles.group}>
        <h2>{t('Màu thương hiệu')}</h2>
        <p className={styles.hint}>{t('Màu thương hiệu: các nút (Track, Home, hotline, nút trên ảnh quảng cáo), thanh TRACKING DETAIL, tiêu đề bảng hành trình và khung tiêu đề quảng cáo. Màu phụ: nền các dòng trong nội dung tracking (thông tin vận đơn, dòng ngày). Để trống = màu mặc định.')}</p>
        <div className={styles.twoColumns}>
          <ColorField form={form} name="brand.primaryColor" label="Màu thương hiệu" fallback="#111111" />
          <ColorField form={form} name="brand.secondaryColor" label="Màu phụ thương hiệu" fallback="#f8f3e1" />
        </div>
      </section>
      <section className={styles.group}>
        <h2>{t('Nội dung hiển thị chính')}</h2>
        <TextField label="Tiêu đề" required maxLength={120} error={errors.title?.message} {...register('title')} />
        <TextAreaField label="Mô tả" rows={4} maxLength={250} error={errors.description?.message} {...register('description')} />
      </section>
      <section className={styles.group}>
        <div className={styles.groupHeading}><h2>{t('Hình ảnh quảng cáo')}</h2><span>{t('Số lượng: {n}/5', { n: fields.length })}</span></div>
        <p className={styles.hint}>{t('Nhập link website cho từng ảnh. Khách bấm vào ảnh hoặc nút sẽ mở website trong tab mới. Tối đa 5 ảnh.')}</p>
        <p className={styles.hint}>{t('Không bắt buộc: không thêm ảnh và để trống mô tả thì cột quảng cáo tự ẩn, khung tra cứu rộng hết trang.')}</p>
        <ImagePicker onAdd={src => {
          if (getValues('images').length < MAX_AD_IMAGES) append({ id: uuid(), src, linkUrl: '', title: '', buttonText: 'Xem thêm' });
        }} disabled={fields.length >= MAX_AD_IMAGES} onBusy={onBusy} />
        <p className={styles.hint}>{t('Kéo tay cầm để đổi thứ tự ảnh hoặc dùng nút lên / xuống.')}</p>
        {fields.length === 0 && <div className={styles.emptyImages}><Icon name="image" size={24} /><span>{t('Chưa có ảnh quảng cáo.')}</span></div>}
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
        <div className={styles.groupHeading}><h2>{t('Cổng liên lạc')}</h2><span>{t('Chọn tối đa {n} mục', { n: MAX_CHANNELS })}</span></div>
        <p className={styles.hint}>{t('Hiện thành các nút liên hệ nổi bên trái trang tracking. Nhập số điện thoại, tên tài khoản hoặc link.')}</p>
        <ChannelListEditor form={form} name="brand.contacts" types={CONTACT_TYPES} />
      </section>
      <section className={styles.group}>
        <div className={styles.groupHeading}><h2>{t('Mạng xã hội')}</h2><span>{t('Chọn tối đa {n} mục', { n: MAX_CHANNELS })}</span></div>
        <p className={styles.hint}>{t('Hiện ở cuối trang tracking (Connect with us). Nhập link trang hoặc tên tài khoản.')}</p>
        <ChannelListEditor form={form} name="brand.socials" types={SOCIAL_TYPES} />
      </section>
      <div className={styles.actions}>
        <Button type="submit" variant="primary">{t(mode === 'local' ? 'Lưu bản thử nghiệm' : 'Lưu')}</Button>
        <Button onClick={onRestore}>{t('Khôi phục mặc định')}</Button>
        <span className={styles.saveStatus} role="status">{t(isDirty ? 'Có thay đổi chưa lưu' : 'Không có thay đổi chưa lưu')}</span>
      </div>
    </fieldset>
  </form>;
}

/** Ô chọn màu: bảng màu của trình duyệt + ô nhập mã #rrggbb; để trống = màu mặc định của trang tracking. */
function ColorField({ form, name, label, fallback }: {
  form: UseFormReturn<MyTrackingConfig>;
  name: 'brand.primaryColor' | 'brand.secondaryColor';
  label: string;
  fallback: string;
}) {
  const { t } = useI18n();
  const value = form.watch(name);
  const set = (next: string) => form.setValue(name, next, { shouldDirty: true, shouldValidate: true });
  const error = name === 'brand.primaryColor' ? form.formState.errors.brand?.primaryColor : form.formState.errors.brand?.secondaryColor;
  return <div className={styles.colorField}>
    <input type="color" className={styles.colorSwatch} value={isHexColor(value) ? value : fallback} aria-label={t(label)}
      onChange={e => set(e.target.value)} />
    <TextField label={label} placeholder={t('Mặc định')} maxLength={7} error={error?.message} {...form.register(name)} />
    <Button size="sm" disabled={!value} onClick={() => set('')}>{t('Mặc định')}</Button>
  </div>;
}
