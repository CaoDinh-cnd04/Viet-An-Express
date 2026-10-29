import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { createOrderSchema, defaultValues, type CreateOrderValues } from '@/features/order-form';

/** Khởi tạo form tạo đơn: zod làm validator; báo lỗi ngay khi rời ô (onTouched), sau đó kiểm tra lại theo từng lần gõ. */
export function useCreateOrderForm(initial?: Partial<CreateOrderValues>) {
  return useForm<CreateOrderValues>({
    resolver: zodResolver(createOrderSchema),
    defaultValues: { ...defaultValues(), ...initial },
    mode: 'onTouched'
  });
}
