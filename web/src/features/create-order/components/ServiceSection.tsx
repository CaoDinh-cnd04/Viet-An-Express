import { useFormContext, useWatch } from 'react-hook-form';
import { useI18n } from '@/shared/i18n';
import { Card, FormGrid, SelectField, TextField } from '@/shared/ui';
import { CARRIERS, defaultHub, hubOptions } from '../constants';
import { useFieldBinder } from '../hooks/useFieldBinder';
import type { CreateOrderValues } from '@/features/order-form';

export function ServiceSection() {
  const bind = useFieldBinder();
  const { lang } = useI18n();
  const { control, setValue, getValues } = useFormContext<CreateOrderValues>();
  const carrier = useWatch({ control, name: 'service.carrier' });
  const hub = useWatch({ control, name: 'service.hub' });
  // Keep an older service/hub visible when copying; only a deliberate carrier change resets the hub.
  const carriers = CARRIERS.includes(carrier) || !carrier ? CARRIERS : [...CARRIERS, carrier];
  const hubs = hubOptions(carrier);
  if (hub && !hubs.some(o => o.value === hub)) hubs.push({ value: hub, label: hub });

  return (
    <Card accent title="Dịch vụ" subtitle={lang === 'vi' ? '(Services)' : undefined}>
      {/* Dịch vụ · Hub · Số tham chiếu trên cùng 1 hàng (màn hình hẹp tự xuống dòng) */}
      <FormGrid columns={3}>
        <SelectField
          label="Dịch vụ"
          required
          placeholder="Chọn dịch vụ"
          options={carriers}
          {...bind('service.carrier', { onChange: () => setValue('service.hub', defaultHub(getValues('service.carrier')), { shouldDirty: true }) })}
        />
        <SelectField label="Hub" required placeholder="Chọn hub" options={hubs} {...bind('service.hub')} />
        <TextField label="Số tham chiếu (mã đơn hàng của bạn)" {...bind('service.reference')} />
      </FormGrid>
    </Card>
  );
}
