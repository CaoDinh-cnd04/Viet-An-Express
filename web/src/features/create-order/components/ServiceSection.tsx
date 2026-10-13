import { useEffect } from 'react';
import { useFormContext, useWatch } from 'react-hook-form';
import { useI18n } from '@/shared/i18n';
import { Card, FormGrid, SelectField, TextField } from '@/shared/ui';
import { CARRIER_HUBS, CARRIERS, defaultHub, hubOptions } from '../constants';
import { useFieldBinder } from '../hooks/useFieldBinder';
import type { CreateOrderValues } from '../schema';

const NO_HUBS: readonly string[] = [];

export function ServiceSection() {
  const bind = useFieldBinder();
  const { lang } = useI18n();
  const { control, setValue, getValues } = useFormContext<CreateOrderValues>();
  const carrier = useWatch({ control, name: 'service.carrier' });
  const hubs = CARRIER_HUBS[carrier] ?? NO_HUBS;

  // Đổi hãng (bằng tay hoặc điền sẵn) → hub hiện tại không thuộc hãng mới thì bỏ chọn (hãng chỉ có 1 hub thì chọn luôn).
  // Chạy sau render để danh sách hub mới đã có trong <select>.
  useEffect(() => {
    if (!hubs.includes(getValues('service.hub'))) setValue('service.hub', defaultHub(carrier), { shouldDirty: true });
  }, [hubs, carrier, getValues, setValue]);

  return (
    <Card accent title="Dịch vụ" subtitle={lang === 'vi' ? '(Services)' : undefined}>
      {/* Dịch vụ · Hub · Số tham chiếu trên cùng 1 hàng (màn hình hẹp tự xuống dòng) */}
      <FormGrid columns={3}>
        <SelectField
          label="Dịch vụ"
          required
          placeholder="Chọn dịch vụ"
          options={CARRIERS}
          {...bind('service.carrier')}
        />
        <SelectField label="Hub" required placeholder="Chọn hub" options={hubOptions(carrier)} {...bind('service.hub')} />
        <TextField label="Số tham chiếu (mã đơn hàng của bạn)" {...bind('service.reference')} />
      </FormGrid>
    </Card>
  );
}
