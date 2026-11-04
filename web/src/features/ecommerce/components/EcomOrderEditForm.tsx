import { zodResolver } from '@hookform/resolvers/zod';
import { useFieldArray, useForm, useWatch } from 'react-hook-form';
import { z } from 'zod';
import { useCountries } from '@/features/create-order';
import { useI18n } from '@/shared/i18n';
import { Button, FormGrid, Icon, Notice, SelectField, TextField } from '@/shared/ui';
import { useUpdateEcomOrder } from '../api';
import { fromEditForm, isLatin, toEditForm, type OrderEditForm } from '../lib/order-edit';
import type { EcomOrder } from '../types';
import styles from './ecommerce.module.css';

const MAX_LINES = 50;
const HS = /^[\d. ]*$/;

const schema = z.object({
  name: z.string().trim().min(1, 'Nhập tên người nhận'),
  company: z.string(),
  phone: z.string(),
  email: z.string(),
  address1: z.string().trim().min(1, 'Nhập địa chỉ'),
  address2: z.string(),
  city: z.string(),
  state: z.string(),
  postal: z.string(),
  countryCode: z.string().length(2, 'Chọn nước đến'),
  kg: z.string().refine(v => v.trim() === '' || Number(v) >= 0, 'Cân nặng không hợp lệ'),
  note: z.string(),
  products: z
    .array(
      z.object({
        name: z.string().trim().min(1, 'Nhập tên hàng'),
        sku: z.string(),
        qty: z.string().refine(v => Number(v) >= 1, 'SL ≥ 1'),
        price: z.string().refine(v => v.trim() !== '' && Number(v) >= 0, 'Nhập giá'),
        hsCode: z.string().refine(v => v.trim() === '' || (HS.test(v) && v.replace(/\D/g, '').length >= 6 && v.replace(/\D/g, '').length <= 10), '6–10 số')
      })
    )
    .min(1)
    .max(MAX_LINES)
}) satisfies z.ZodType<OrderEditForm>;

/** Sửa đơn chưa có bill: người nhận (viết lại bằng chữ Latin), cân nặng, sản phẩm + mã HS. */
export function EcomOrderEditForm({ order, onDone }: { order: EcomOrder; onDone: () => void }) {
  const { t } = useI18n();
  const save = useUpdateEcomOrder();
  const countries = useCountries();
  const { control, register, handleSubmit, formState } = useForm<OrderEditForm>({ resolver: zodResolver(schema), defaultValues: toEditForm(order) });
  const { fields, append, remove } = useFieldArray({ control, name: 'products' });
  const values = useWatch({ control });
  const e = formState.errors;

  const current = order.receiver?.countryCode;
  const countryOptions = [
    { value: '', label: t('Chọn nước') },
    ...(countries.data?.length ? countries.data.filter(c => c.code.length === 2) : current ? [{ code: current, name: order.ct || current }] : [])
      .map(c => ({ value: c.code, label: c.name }))
  ];
  /** Gợi ý dưới ô còn chữ Nhật / Hàn / Trung… */
  const latinHint = (v: string | undefined) => (v && !isLatin(v) ? t('Chưa phải chữ Latin — nhãn hãng bay không in được') : undefined);

  const submit = handleSubmit(f => save.mutate({ id: order.id, body: fromEditForm(f, order) }, { onSuccess: onDone }));

  return (
    <form onSubmit={ev => void submit(ev)} noValidate className={styles.formSections}>
      <Notice tone="info">
        {t('Nhãn DHL / FedEx / UPS chỉ in chữ Latin: địa chỉ tiếng Nhật, Hàn, Trung… cần viết lại (vd romaji). Sau khi lưu, đồng bộ lại từ sàn sẽ không ghi đè đơn này.')}
      </Notice>

      <section>
        <h3 className={styles.subTitle}>{t('Người nhận')}</h3>
        <FormGrid columns={2}>
          <TextField label="Tên người nhận" required error={e.name?.message} hint={latinHint(values.name)} {...register('name')} />
          <TextField label="Công ty" hint={latinHint(values.company)} {...register('company')} />
          <TextField label="Điện thoại" type="tel" hint="Kèm mã nước, vd +81…; số nội địa sẽ tự thêm mã nước" {...register('phone')} />
          <TextField label="Email" type="email" {...register('email')} />
          <TextField label="Địa chỉ" required className={styles.fullRow} error={e.address1?.message} hint={latinHint(values.address1)} {...register('address1')} />
          <TextField label="Địa chỉ (dòng 2)" className={styles.fullRow} hint={latinHint(values.address2)} {...register('address2')} />
          <TextField label="Thành phố" hint={latinHint(values.city)} {...register('city')} />
          <TextField label="Tỉnh / bang" hint={latinHint(values.state)} {...register('state')} />
          <TextField label="Mã bưu chính" {...register('postal')} />
          <SelectField label="Nước đến" required options={countryOptions} error={e.countryCode?.message} {...register('countryCode')} />
        </FormGrid>
      </section>

      <section>
        <h3 className={styles.subTitle}>{t('Hàng hóa')}</h3>
        <FormGrid columns={2}>
          <TextField label="Cân nặng" type="number" min={0} step="any" suffix="kg" hint="Để trống nếu Việt An cân" error={e.kg?.message} {...register('kg')} />
        </FormGrid>
        <div className={styles.productList}>
          {fields.map((f, i) => {
            const pe = e.products?.[i];
            return (
              <div key={f.id} className={styles.editProductRow}>
                <TextField label="Tên hàng (EN)" required error={pe?.name?.message} hint={latinHint(values.products?.[i]?.name)} {...register(`products.${i}.name`)} />
                <TextField label="SKU" {...register(`products.${i}.sku`)} />
                <TextField label="SL" type="number" min={1} error={pe?.qty?.message} {...register(`products.${i}.qty`)} />
                <TextField label="Đơn giá" type="number" min={0} step="any" error={pe?.price?.message} {...register(`products.${i}.price`)} />
                <TextField label="Mã HS" placeholder="6109.10" error={pe?.hsCode?.message} {...register(`products.${i}.hsCode`)} />
                <Button iconOnly variant="ghost" aria-label={t('Xóa sản phẩm {n}', { n: i + 1 })} disabled={fields.length <= 1} onClick={() => remove(i)}>
                  <Icon name="close" size={15} />
                </Button>
              </div>
            );
          })}
        </div>
        <Button size="sm" disabled={fields.length >= MAX_LINES} onClick={() => append({ name: '', sku: '', qty: '1', price: '', hsCode: '' })}>
          {t('Thêm sản phẩm')}
        </Button>
      </section>

      <TextField label="Ghi chú" {...register('note')} />

      <div className={styles.formActions}>
        <Button onClick={onDone}>{t('Hủy')}</Button>
        <Button variant="primary" type="submit" disabled={save.isPending}>
          {t(save.isPending ? 'Đang lưu…' : 'Lưu thay đổi')}
        </Button>
      </div>
    </form>
  );
}
