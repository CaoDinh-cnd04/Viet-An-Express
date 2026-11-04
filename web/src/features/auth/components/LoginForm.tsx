import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { z } from 'zod';
import { useI18n } from '@/shared/i18n';
import { Button, LinkButton, Notice, TextField } from '@/shared/ui';
import { isOtpChallenge, useLogin, useLogout, useSession, useSetOtpEmail, useVietAnLogin } from '../api';
import type { OtpChallenge } from '../types';
import { IDLE_TIMEOUT_MS } from '../lib/idle';
import { CHOOSE_CUSTOMER_PATH, isVietAnAgent, safeNextPath } from '../lib/session';
import { OtpEmailStep } from './OtpEmailStep';
import { OtpStep } from './OtpStep';
import styles from './LoginForm.module.css';

const schema = z.object({
  username: z.string().trim().min(1, 'Nhập tên đăng nhập'),
  password: z.string().min(1, 'Nhập mật khẩu'),
  remember: z.boolean()
});
type FormValues = z.infer<typeof schema>;

/** Khách hàng (dbo.TCustomer / tài khoản con) hoặc nhân viên Việt An (dbo.USERS) làm thay khách. */
type Mode = 'customer' | 'vietan';

/**
 * Form đăng nhập portal (không kèm khung). Đã đăng nhập thì hiện nút vào portal. `next` = trang quay lại (mặc định lấy ?next=).
 * Link nhỏ cuối form chuyển sang "Nhân viên Việt An": đăng nhập tài khoản dbo.USERS, nhập mã OTP gửi email rồi chọn khách để tạo đơn giúp
 * (không cần mật khẩu của khách).
 */
export function LoginForm({ autoFocus, next: nextPath }: { autoFocus?: boolean; next?: string }) {
  const { t } = useI18n();
  const session = useSession();
  const [mode, setMode] = useState<Mode>('customer');
  const customerLogin = useLogin();
  const vietAnLogin = useVietAnLogin();
  const login = mode === 'vietan' ? vietAnLogin : customerLogin;
  /** Đúng mật khẩu → bước nhập mã OTP (nhân viên Việt An luôn có; khách khi bật OTP khách). */
  const [otp, setOtp] = useState<{ challenge: OtpChallenge; values: FormValues; email?: string } | null>(null);
  /** Gửi lại mã xác nhận email lần đầu (cùng email, cùng đồng ý). */
  const resendEmail = useSetOtpEmail();
  const logout = useLogout();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const next = safeNextPath(nextPath ?? params.get('next'));

  const { register, handleSubmit, formState } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { username: '', password: '', remember: true }
  });
  const err = (k: keyof FormValues) => formState.errors[k]?.message;
  const loginCustomer = (values: FormValues) =>
    customerLogin.mutate(values, {
      onSuccess: data => (isOtpChallenge(data) ? setOtp({ challenge: data, values }) : void navigate(next, { replace: true }))
    });
  const requestOtp = (values: FormValues) =>
    mode === 'vietan' ? vietAnLogin.mutate(values, { onSuccess: challenge => setOtp({ challenge, values }) }) : loginCustomer(values);
  const submit = handleSubmit(requestOtp);

  const status = session.data?.status;
  const user = session.data?.status === 'authenticated' ? session.data.user : undefined;

  if (user) {
    return (
      <div className="page-stack">
        <Notice tone="success" title={t('Xin chào, {name}', { name: isVietAnAgent(user) ? (user.agentName ?? '') : user.companyName })}>
          {isVietAnAgent(user)
            ? t('Bạn đang đăng nhập bằng tài khoản nhân viên Việt An.')
            : t('Bạn đang đăng nhập với mã khách hàng {code}.', { code: user.customerCode })}
        </Notice>
        <LinkButton to={next} variant="primary" className={styles.full}>
          {t('Vào portal')}
        </LinkButton>
        <Button variant="ghost" className={styles.full} disabled={logout.isPending} onClick={() => logout.mutate()}>
          {t('Đăng nhập tài khoản khác')}
        </Button>
      </div>
    );
  }

  if (otp?.challenge.needEmail) {
    return (
      <OtpEmailStep
        challenge={otp.challenge}
        onSent={(challenge, email) => setOtp({ ...otp, challenge, email })}
        onBack={() => setOtp(null)}
      />
    );
  }

  if (otp) {
    const current = otp;
    return (
      <OtpStep
        kind={mode}
        challenge={current.challenge}
        resending={login.isPending || resendEmail.isPending}
        onResend={() =>
          current.email
            ? resendEmail.mutate(
                { challengeId: current.challenge.challengeId, email: current.email, acceptTerms: true },
                { onSuccess: challenge => setOtp({ ...current, challenge }) }
              )
            : requestOtp(current.values)}
        onBack={() => setOtp(null)}
        onVerified={() =>
          void navigate(mode === 'vietan' ? `${CHOOSE_CUSTOMER_PATH}?next=${encodeURIComponent(next)}` : next, { replace: true })}
      />
    );
  }

  return (
    <form onSubmit={e => void submit(e)} noValidate className="page-stack">
      {params.get('idle') === '1' && (
        <Notice tone="info">
          {t('Bạn đã được tự đăng xuất do không thao tác trong {n} phút. Vui lòng đăng nhập lại.', { n: IDLE_TIMEOUT_MS / 60_000 })}
          {next === '/drafts' && <> {t('Đơn đang tạo dở đã được lưu vào Đơn nháp & chưa in.')}</>}
        </Notice>
      )}
      {mode === 'vietan' && <p className={styles.mode}>{t('Đăng nhập nhân viên Việt An')}</p>}
      {status === 'open' && (
        <Notice title="Chưa kết nối máy chủ đăng nhập">{t('Bạn có thể vào portal để dùng thử trong lúc chờ kết nối.')}</Notice>
      )}
      <TextField
        label={mode === 'vietan' ? 'Tên đăng nhập nhân viên' : 'Tên đăng nhập'}
        required
        autoComplete="username"
        autoFocus={autoFocus}
        error={err('username')}
        {...register('username')}
      />
      <TextField label="Mật khẩu" required type="password" autoComplete="current-password" error={err('password')} {...register('password')} />
      <label className={styles.remember}>
        <input type="checkbox" {...register('remember')} />
        {t('Ghi nhớ đăng nhập trên thiết bị này')}
      </label>
      <Button variant="primary" type="submit" className={styles.full} disabled={login.isPending}>
        {t(login.isPending ? 'Đang đăng nhập…' : 'Đăng nhập')}
      </Button>
      {status === 'open' && <LinkButton to={next} className={styles.full}>{t('Vào portal dùng thử')}</LinkButton>}
      <p className={styles.foot}>
        {t(mode === 'vietan'
          ? 'Dùng tài khoản nhân viên trên phần mềm Việt An. Mã OTP sẽ gửi tới email của bạn; xác nhận xong thì chọn khách để tạo đơn giúp.'
          : 'Quên mật khẩu hoặc chưa có tài khoản? Liên hệ nhân viên kinh doanh Việt An để được cấp.')}{' '}
        <button type="button" className={styles.switch} onClick={() => setMode(m => (m === 'vietan' ? 'customer' : 'vietan'))}>
          {t(mode === 'vietan' ? 'Đăng nhập khách hàng' : 'Nhân viên Việt An đăng nhập')}
        </button>
      </p>
    </form>
  );
}
