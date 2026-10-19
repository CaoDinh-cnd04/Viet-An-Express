import { z } from 'zod';
import { PASSWORD_MESSAGE, PASSWORD_RULE } from '@/shared/lib/password';
import { isPhone, PHONE_MESSAGE } from '@/shared/lib/phone';

/** Tên đăng nhập — đồng bộ với backend (StaffAccount.UserNamePattern, 3–50 ký tự). */
export const USER_NAME_RULE = /^[A-Za-z0-9._@-]{3,50}$/;

const profile = {
  fullName: z.string().trim().min(1, 'Nhập họ tên nhân viên').max(100, 'Họ tên tối đa 100 ký tự'),
  email: z.union([z.literal(''), z.email('Email không hợp lệ').max(150)]),
  phone: z.string().trim().max(30, 'Số điện thoại tối đa 30 ký tự').refine(v => !v || isPhone(v), PHONE_MESSAGE),
  phone2: z.string().trim().max(30, 'Số điện thoại tối đa 30 ký tự').refine(v => !v || isPhone(v), PHONE_MESSAGE),
  email2: z.union([z.literal(''), z.email('Email không hợp lệ').max(150)]),
  permissions: z.array(z.string())
};

export const staffSchema = z
  .object({
    ...profile,
    /** Chỉ dùng khi tạo mới. */
    userName: z.string().trim(),
    password: z.string(),
    confirmPassword: z.string(),
    isNew: z.boolean()
  })
  .superRefine((v, ctx) => {
    if (!v.isNew) return;
    if (!USER_NAME_RULE.test(v.userName))
      ctx.addIssue({ code: 'custom', path: ['userName'], message: 'Từ 3 đến 50 ký tự: chữ không dấu, số và . _ - @' });
    if (!PASSWORD_RULE.test(v.password)) ctx.addIssue({ code: 'custom', path: ['password'], message: PASSWORD_MESSAGE });
    if (v.password !== v.confirmPassword) ctx.addIssue({ code: 'custom', path: ['confirmPassword'], message: 'Mật khẩu nhập lại không khớp' });
  });
export type StaffFormValues = z.infer<typeof staffSchema>;

export const resetPasswordSchema = z
  .object({ newPassword: z.string().regex(PASSWORD_RULE, PASSWORD_MESSAGE), confirmPassword: z.string() })
  .refine(v => v.newPassword === v.confirmPassword, { path: ['confirmPassword'], message: 'Mật khẩu nhập lại không khớp' });
export type ResetPasswordValues = z.infer<typeof resetPasswordSchema>;
