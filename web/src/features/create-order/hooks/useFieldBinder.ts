import { get, useFormContext, type Path, type RegisterOptions } from 'react-hook-form';
import type { CreateOrderValues } from '@/features/order-form';

export type FieldName = Path<CreateOrderValues>;

/**
 * Gắn nhanh một ô nhập vào form: <TextField label="…" {...bind('shipper.company')} />
 * Trả về props của register + `error` (thông báo lỗi của trường, nếu có).
 */
export function useFieldBinder() {
  const { register, formState } = useFormContext<CreateOrderValues>();
  return (name: FieldName, options?: RegisterOptions<CreateOrderValues, FieldName>) => ({
    ...register(name, options),
    error: get(formState.errors, name)?.message as string | undefined
  });
}
