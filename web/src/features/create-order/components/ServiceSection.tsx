import { useEffect } from 'react';
import { useFormContext, useWatch } from 'react-hook-form';
import { SelectField, TextField } from '@/shared/ui';
import { carriersFor, defaultHub, hubOptions } from '../constants';
import { useFieldBinder } from '../hooks/useFieldBinder';
import { useServiceCatalog, type CreateOrderValues } from '@/features/order-form';
import styles from './form.module.css';

/** id ô Nước đến — khung Người nhận có nút "Đổi" nhảy về ô này. */
export const COUNTRY_INPUT_ID = 'order-country';

/**
 * Nước đến · Dịch vụ · Hub trên 1 hàng (mẫu B, 04/11/2026) + Số tham chiếu — đầu khung "Hàng & dịch vụ".
 * Chọn nước trước: chưa có nước thì Dịch vụ / Hub chưa có lựa chọn; có nước thì chỉ hiện hãng / hub đi được nước đó
 * (bảng CS điền + nước hãng không phục vụ, shared/config/hub-countries.ts). Đổi nước làm dịch vụ / hub đang chọn không còn hợp → bỏ chọn.
 * Gợi ý tên nước (datalist "va-countries"), mã điện thoại, xoá địa chỉ khi đổi nước: ReceiverSection (theo dõi giá trị receiver.country).
 */
export function ServiceSection() {
  const bind = useFieldBinder();
  // Danh mục hãng → hub đọc từ bảng hệ thống cũ (dự phòng: danh sách chép tay khi chưa đọc được).
  const catalog = useServiceCatalog();
  const { control, setValue, getValues } = useFormContext<CreateOrderValues>();
  const [carrier, hub, countryText, countryCode] = useWatch({
    control,
    name: ['service.carrier', 'service.hub', 'receiver.country', 'receiver.countryCode']
  });
  const noCountry = !countryText?.trim();
  const code = countryCode && countryCode.length === 2 ? countryCode : undefined;
  // Chưa chọn nước: danh sách trống (không dùng disabled — react-hook-form bỏ giá trị ô bị khóa khi kiểm tra).
  const available = noCountry ? [] : carriersFor(code, catalog);
  // Đơn copy / nháp cũ giữ dịch vụ đã lưu dù không còn trong danh sách (khách tự đổi nếu muốn).
  const carriers = available.includes(carrier) || !carrier ? available : [...available, carrier];
  const hubs = noCountry ? [] : hubOptions(carrier, code, catalog);
  if (hub && !hubs.some(o => o.value === hub)) hubs.push({ value: hub, label: hub });

  useEffect(() => {
    if (!code) return;
    const current = getValues('service.carrier');
    if (current && !carriersFor(code, catalog).includes(current)) {
      setValue('service.carrier', '', { shouldDirty: true });
      setValue('service.hub', '', { shouldDirty: true });
    } else if (current && !hubOptions(current, code, catalog).some(o => o.value === getValues('service.hub'))) {
      setValue('service.hub', defaultHub(current, code, catalog), { shouldDirty: true });
    }
  }, [code, catalog, getValues, setValue]);

  return (
    <div className={styles.serviceGrid}>
      <TextField
        id={COUNTRY_INPUT_ID}
        label="Nước đến"
        required
        list="va-countries"
        autoComplete="country-name"
        placeholder="Chọn nước"
        {...bind('receiver.country')}
      />
      <SelectField
        label="Dịch vụ"
        required
        placeholder={noCountry ? 'Chọn nước trước' : 'Chọn dịch vụ'}
        options={carriers}
        {...bind('service.carrier', { onChange: () => setValue('service.hub', defaultHub(getValues('service.carrier'), code, catalog), { shouldDirty: true }) })}
      />
      <SelectField label="Hub" required placeholder="Chọn hub" options={hubs} {...bind('service.hub')} />
      <TextField className={styles.serviceRef} label="Số tham chiếu" placeholder="Mã đơn hàng của bạn" {...bind('service.reference')} />
    </div>
  );
}
