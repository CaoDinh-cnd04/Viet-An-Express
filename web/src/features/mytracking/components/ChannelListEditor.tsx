import { useFieldArray, type UseFormReturn } from 'react-hook-form';
import { useI18n } from '@/shared/i18n';
import { Button, Icon, SelectField, TextField } from '@/shared/ui';
import { CHANNELS, MAX_CHANNELS, type ContactType, type SocialType } from '../lib/channels';
import type { MyTrackingConfig } from '../schema';
import styles from '../pages/MyTrackingPage.module.css';

type Props = { form: UseFormReturn<MyTrackingConfig> } & (
  | { name: 'brand.contacts'; types: readonly ContactType[] }
  | { name: 'brand.socials'; types: readonly SocialType[] }
);

/** Danh sách cổng liên lạc / mạng xã hội: mỗi dòng chọn loại + nhập giá trị; tối đa 5 dòng, không trùng loại. */
export function ChannelListEditor({ form, name, types }: Props) {
  const { t } = useI18n();
  const { control, register, watch, formState: { errors } } = form;
  const { fields, append, remove } = useFieldArray({ control, name: name as 'brand.contacts' });
  const rows = watch(name) ?? [];
  const used = new Set(rows.map(r => r.type));
  const free = types.filter(type => !used.has(type));
  const listErrors = name === 'brand.contacts' ? errors.brand?.contacts : errors.brand?.socials;

  return <div className={styles.channelList}>
    {fields.map((field, index) => {
      const type = rows[index]?.type ?? field.type;
      const options = types.filter(option => option === type || !used.has(option)).map(option => ({ value: option, label: CHANNELS[option].label }));
      return <div key={field.id} className={styles.channelRow}>
        <SelectField label={t('Loại {n}', { n: index + 1 })} options={options} {...register(`${name}.${index}.type` as 'brand.contacts.0.type')} />
        <TextField label="Thông tin" placeholder={t(CHANNELS[type].placeholder)}
          error={listErrors?.[index]?.value?.message ?? listErrors?.[index]?.type?.message}
          {...register(`${name}.${index}.value` as 'brand.contacts.0.value')} />
        <div className={styles.channelActions}>
          <Button size="sm" variant="danger" aria-label={t('Xóa {name}', { name: CHANNELS[type].label })} onClick={() => remove(index)}>{t('Xóa')}</Button>
        </div>
      </div>;
    })}
    {fields.length === 0 && <p className={styles.hint}>{t('Chưa chọn mục nào — phần này sẽ ẩn trên trang tracking.')}</p>}
    <div>
      <Button size="sm" disabled={fields.length >= MAX_CHANNELS || free.length === 0}
        onClick={() => append({ type: free[0] as ContactType, value: '' })}>
        <Icon name="plus" size={14} /> {t('Thêm mục ({n}/{max})', { n: fields.length, max: MAX_CHANNELS })}
      </Button>
    </div>
  </div>;
}
