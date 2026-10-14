import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useFieldArray, useForm, useWatch } from 'react-hook-form';
import { z } from 'zod';
import { COUNTRIES } from '@/shared/config/domain';
import { findCountry, normalizePostal, shouldResetAddress, useCountries, usePostalSearch, usePostalPlaceBox } from '@/features/create-order';
import { useI18n } from '@/shared/i18n';
import { useDebouncedCallback } from '@/shared/lib/useDebouncedCallback';
import { Button, Card, FormGrid, Icon, SelectField, TextField } from '@/shared/ui';
import { useCreateManualEcomOrder } from '../api';
import { ECOM_SOURCES, MAX_PRODUCTS } from '../constants';
import { ECOM_ORDERS_PATH } from '../lib/store-connection';
import type { EcomSource } from '../types';
import styles from './ecommerce.module.css';

const product = z.object({
  name: z.string().trim().min(1, 'Nhập tên hàng'),
  sku: z.string(),
  qty: z.string().refine(v => Number(v) >= 1, 'SL ≥ 1'),
  price: z.string().refine(v => v.trim() !== '' && Number(v) >= 0, 'Nhập giá')
});

/** Chỉ các trường đơn sàn nào cũng có — dịch vụ, hub, mã HS, khai hải quan do Việt An bổ sung khi nhận hàng. */
const schema = z.object({
  ref: z.string().trim().min(1, 'Nhập mã đơn của shop'),
  source: z.string(),
  cnee: z.string().trim().min(1, 'Nhập tên người nhận'),
  phone: z.string().trim().min(1, 'Nhập số điện thoại'),
  email: z.string(),
  ct: z.string().trim().min(1, 'Nhập nước đến'),
  postal: z.string(),
  city: z.string(),
  state: z.string(),
  address: z.string().trim().min(1, 'Nhập địa chỉ'),
  kg: z.string(),
  products: z.array(product).min(1).max(MAX_PRODUCTS)
});
type FormValues = z.infer<typeof schema>;

const emptyProduct = (): FormValues['products'][number] => ({ name: '', sku: '', qty: '1', price: '' });
const defaults = (): FormValues => ({
  ref: '', source: 'manual', cnee: '', phone: '', email: '', ct: '', postal: '', city: '', state: '', address: '', kg: '',
  products: [emptyProduct()]
});

/** Nguồn chọn được khi nhập tay: tự nhập, hoặc đơn bán trên 1 sàn chưa kết nối (không có API / Excel — đó là cách nhập khác). */
const MANUAL_SOURCES: ReadonlyArray<EcomSource> = ['manual', 'tiktok', 'shopee', 'lazada', 'amazon', 'ebay', 'etsy', 'woocommerce'];
const SOURCE_OPTIONS = MANUAL_SOURCES.map(k => ({ value: k, label: ECOM_SOURCES[k].label }));

/** Thêm 1 đơn bán ngoài sàn đã kết nối (1–5 sản phẩm). Lưu xong vào thẳng trang "Đơn hàng E-com". */
export function ManualEcomForm() {
  const { t } = useI18n();
  const navigate = useNavigate();
  const create = useCreateManualEcomOrder();
  const { control, register, handleSubmit, reset, setValue, setError, clearErrors, formState } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: defaults() });
  const { fields, append, remove } = useFieldArray({ control, name: 'products' });
  const e = formState.errors;

  // ---------- Nước đến: danh sách từ GET /geo/countries; mã bưu chính → thành phố, bang qua GeoNames ----------
  const countries = useCountries();
  // API lỗi → danh sách tĩnh (mã = tên) để form vẫn dùng được.
  const countryList = useMemo(() => (countries.data?.length ? countries.data : COUNTRIES.map(name => ({ code: name, name }))), [countries.data]);
  const [countryText = '', postalText = ''] = useWatch({ control, name: ['ct', 'postal'] });
  const country = findCountry(countryList, countryText);
  const isoCode = country?.code.length === 2 ? country.code : undefined;

  const [postalQuery, setPostalQuery] = useState<string | null>(null);
  const schedulePostal = useDebouncedCallback((value: string | null) => setPostalQuery(value), 500);
  useEffect(() => schedulePostal(normalizePostal(postalText)), [postalText, schedulePostal]);
  const postal = usePostalSearch(isoCode, postalQuery);
  // Khách gõ mã → gợi ý mã + thành phố (GeoNames); chọn → điền mã + thành phố.
  const postalBox = usePostalPlaceBox(postal.data, o => {
    setValue('postal', o.postalCode, { shouldDirty: true });
    setValue('city', o.city, { shouldDirty: true });
  });

  // Đổi sang nước khác → xoá mã bưu chính, thành phố, bang của nước cũ.
  const lastCountry = useRef<string | undefined>(undefined);
  const onCountryInput = (ev: { target: { value: string } }) => {
    const next = findCountry(countryList, ev.target.value)?.code;
    if (shouldResetAddress(lastCountry.current, next)) {
      for (const f of ['postal', 'city', 'state'] as const) setValue(f, '', { shouldDirty: true });
      setPostalQuery(null);
    }
    if (next) {
      lastCountry.current = next;
      clearErrors('ct');
    }
  };

  const postalHint = isoCode && postalQuery && !postal.isFetching && postal.data?.length === 0
    ? 'Không tìm thấy mã này — vui lòng tự nhập thành phố, tỉnh / bang'
    : undefined;

  const submit = handleSubmit(v => {
    const picked = findCountry(countryList, v.ct);
    if (!picked) {
      setError('ct', { message: 'Chọn nước trong danh sách' });
      return;
    }
    create.mutate(
      {
        ref: v.ref,
        source: v.source as EcomSource,
        cnee: v.cnee,
        phone: v.phone,
        email: v.email.trim() || undefined,
        ct: picked.name,
        countryCode: picked.code.length === 2 ? picked.code : undefined,
        postal: v.postal,
        city: v.city,
        state: v.state,
        address: v.address,
        kg: Number(v.kg) || 0,
        // Đơn sàn chỉ có giá bán — khai giá FOB bằng giá bán.
        products: v.products.map(p => ({ name: p.name, sku: p.sku, qty: Number(p.qty), fobPrice: Number(p.price), sellingPrice: Number(p.price) }))
      },
      {
        onSuccess: () => {
          reset(defaults());
          lastCountry.current = undefined;
          setPostalQuery(null);
          navigate(ECOM_ORDERS_PATH);
        }
      }
    );
  });

  return (
    <Card title="Thêm 1 đơn" subtitle="· 1–5 sản phẩm, lưu vào Đơn hàng E-com">
      <form onSubmit={ev => void submit(ev)} noValidate className={styles.formSections}>
        {/* Bố cục dọc: người nhận → đơn hàng → sản phẩm; lưới 4 cột để cả form nằm gọn trong 1 màn hình */}
        <section>
          <h3 className={styles.subTitle}>{t('Người nhận')}</h3>
          <FormGrid columns={4}>
            <TextField label="Tên người nhận" required error={e.cnee?.message} {...register('cnee')} />
            <TextField label="Điện thoại" type="tel" required placeholder="Kèm mã nước, vd +81…" error={e.phone?.message} {...register('phone')} />
            {/* Hàng 1: tên · điện thoại · email; hàng 2: nước · mã bưu chính · thành phố · tỉnh; hàng 3: địa chỉ */}
            <TextField label="Email" type="email" className={styles.span2} {...register('email')} />
            <TextField
              label="Nước đến"
              required
              list="va-ecom-countries"
              autoComplete="country-name"
              placeholder={countries.isLoading ? t('Đang tải danh sách nước…') : t('Gõ để tìm nước')}
              error={e.ct?.message}
              {...register('ct', { onChange: onCountryInput })}
            />
            <div className={styles.suggestWrap}>
              <TextField label="Mã bưu chính" hint={postalHint} {...postalBox.inputProps} {...register('postal', { onChange: postalBox.onType, onBlur: postalBox.onBlur })} />
              {postalBox.list}
            </div>
            <TextField label="Thành phố" {...register('city')} />
            <TextField label="Tỉnh / bang" {...register('state')} />
            <TextField label="Địa chỉ người nhận" required className={styles.fullRow} error={e.address?.message} {...register('address')} />
          </FormGrid>
          <datalist id="va-ecom-countries">{countryList.map(c => <option key={c.code} value={c.name} />)}</datalist>
        </section>

        <section>
          <h3 className={styles.subTitle}>{t('Đơn hàng')}</h3>
          <FormGrid columns={4}>
            <TextField label="Mã đơn của shop (REF)" required error={e.ref?.message} {...register('ref')} />
            <SelectField label="Bán trên" options={SOURCE_OPTIONS} {...register('source')} />
            <TextField label="Cân nặng" type="number" min={0} step="any" suffix="kg" placeholder="Trống: Việt An cân" {...register('kg')} />
          </FormGrid>
        </section>

        <section>
          <h3 className={styles.subTitle}>{t('Sản phẩm ({n}/{max})', { n: fields.length, max: MAX_PRODUCTS })}</h3>
          <div className={styles.productList}>
            {fields.map((f, i) => {
              const pe = e.products?.[i];
              return (
                <div key={f.id} className={styles.manualProductRow}>
                  <TextField label="Tên hàng" required error={pe?.name?.message} {...register(`products.${i}.name`)} />
                  <TextField label="SKU" {...register(`products.${i}.sku`)} />
                  <TextField label="SL" type="number" min={1} error={pe?.qty?.message} {...register(`products.${i}.qty`)} />
                  <TextField label="Giá bán" type="number" min={0} step="any" error={pe?.price?.message} {...register(`products.${i}.price`)} />
                  <Button iconOnly variant="ghost" aria-label={t('Xóa sản phẩm {n}', { n: i + 1 })} disabled={fields.length <= 1} onClick={() => remove(i)}>
                    <Icon name="close" size={15} />
                  </Button>
                </div>
              );
            })}
          </div>
          <Button size="sm" disabled={fields.length >= MAX_PRODUCTS} onClick={() => append(emptyProduct())}>
            <Icon name="plus" size={15} /> {t('Thêm sản phẩm')}
          </Button>
        </section>

        <div className={styles.formActions}>
          <Button onClick={() => reset(defaults())}>{t('Làm mới')}</Button>
          <Button variant="primary" type="submit" disabled={create.isPending}>
            <Icon name="check" size={15} /> {t(create.isPending ? 'Đang lưu…' : 'Lưu đơn')}
          </Button>
        </div>
      </form>
    </Card>
  );
}
