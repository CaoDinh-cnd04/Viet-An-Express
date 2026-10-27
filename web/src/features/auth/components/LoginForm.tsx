import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { z } from 'zod';
import { useI18n } from '@/shared/i18n';
import { Button, Icon, LinkButton, Notice, TextField } from '@/shared/ui';
import { useLogin, useLogout, useSession } from '../api';
import { IDLE_TIMEOUT_MS } from '../lib/idle';
import { safeNextPath } from '../lib/session';
import styles from './LoginForm.module.css';

const schema = z.object({
  username: z.string().trim().min(1, 'Nhập tên đăng nhập'),
  password: z.string().min(1, 'Nhập mật khẩu'),
  remember: z.boolean()
});
type FormValues = z.infer<typeof schema>;

/** Form đăng nhập portal (không kèm khung). Đã đăng nhập thì hiện nút vào portal. `next` = trang quay lại (mặc định lấy ?next=). */
export function LoginForm({ autoFocus, next: nextPath }: { autoFocus?: boolean; next?: string }) {
  const { t } = useI18n();
  const session = useSession();
  const login = useLogin();
  const logout = useLogout();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const next = safeNextPath(nextPath ?? params.get('next'));

  const { register, handleSubmit, formState } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { username: '', password: '', remember: true }
  });
  const err = (k: keyof FormValues) => formState.errors[k]?.message;
  const submit = handleSubmit(values => login.mutate(values, { onSuccess: () => void navigate(next, { replace: true }) }));

  const status = session.data?.status;
  const user = session.data?.status === 'authenticated' ? session.data.user : undefined;

  if (user) {
    return (
      <div className="page-stack">
        <Notice tone="success" title={t('Xin chào, {name}', { name: user.companyName })}>
          {t('Bạn đang đăng nhập với mã khách hàng {code}.', { code: user.customerCode })}
        </Notice>
        <LinkButton to={next} variant="primary" className={styles.full}>
          {t('Vào portal')} <Icon name="chevronRight" size={16} />
        </LinkButton>
        <Button variant="ghost" className={styles.full} disabled={logout.isPending} onClick={() => logout.mutate()}>
          {t('Đăng nhập tài khoản khác')}
        </Button>
      </div>
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
      {status === 'open' && (
        <Notice title="Chưa kết nối máy chủ đăng nhập">{t('Bạn có thể vào portal để dùng thử trong lúc chờ kết nối.')}</Notice>
      )}
      <TextField
        label="Tên đăng nhập"
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
      <p className={styles.foot}>{t('Quên mật khẩu hoặc chưa có tài khoản? Liên hệ nhân viên kinh doanh Việt An để được cấp.')}</p>
    </form>
  );
}
