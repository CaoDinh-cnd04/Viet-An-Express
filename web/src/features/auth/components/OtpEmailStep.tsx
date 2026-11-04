import { useState } from 'react';
import { TermsDialog } from '@/features/terms';
import { useI18n } from '@/shared/i18n';
import { Button, TextField } from '@/shared/ui';
import { useSetOtpEmail } from '../api';
import type { OtpChallenge } from '../types';
import styles from './LoginForm.module.css';

interface Props {
  /** Thử thách từ /auth/login (needEmail) — giữ chỗ 5 phút cho bước này. */
  challenge: OtpChallenge;
  /** Đã gửi mã xác nhận tới email vừa nhập → sang bước nhập mã. */
  onSent: (challenge: OtpChallenge, email: string) => void;
  onBack: () => void;
}

/**
 * Lần đăng nhập đầu khi bật OTP khách: nhập email nhận mã đăng nhập (điền sẵn email hồ sơ nếu có) + đồng ý xử lý dữ liệu cá nhân
 * (Luật Bảo vệ dữ liệu cá nhân 2025, Điều khoản dịch vụ mục 15). Backend gửi mã tới email đó; nhập đúng mã mới lưu email và vào portal.
 */
export function OtpEmailStep({ challenge, onSent, onBack }: Props) {
  const { t } = useI18n();
  const setEmail = useSetOtpEmail();
  const [email, setEmailText] = useState(challenge.suggestedEmail ?? '');
  const [agreed, setAgreed] = useState(false);
  const [terms, setTerms] = useState(false);
  const [emailError, setEmailError] = useState<string>();
  const [consentError, setConsentError] = useState(false);

  const submit = () => {
    const value = email.trim();
    const validEmail = /^[^\s@,;]+@[^\s@,;]+\.[^\s@,;]+$/.test(value);
    setEmailError(validEmail ? undefined : 'Nhập đúng 1 địa chỉ email');
    setConsentError(!agreed);
    if (!validEmail || !agreed) return;
    setEmail.mutate(
      { challengeId: challenge.challengeId, email: value, acceptTerms: true },
      { onSuccess: next => onSent(next, value) }
    );
  };

  return (
    <form onSubmit={e => { e.preventDefault(); submit(); }} noValidate className="page-stack">
      <p className={styles.mode}>{t('Đăng ký email nhận mã đăng nhập')}</p>
      <p className={styles.otpSent}>
        {t('Lần đầu đăng nhập: nhập email nhận mã OTP. Từ lần sau, mỗi lần đăng nhập bằng mật khẩu, mã sẽ gửi tới email này.')}
      </p>
      <TextField
        label="Email nhận mã"
        required
        type="email"
        autoComplete="email"
        autoFocus
        value={email}
        onChange={e => setEmailText(e.target.value)}
        error={emailError}
      />
      <div className={styles.consent}>
        <p>
          {t('Việt An Express thu thập và xử lý email này để gửi mã đăng nhập (OTP) và thông báo liên quan tới tài khoản của bạn; email được lưu vào hồ sơ khách hàng cho tới khi bạn đổi email hoặc ngừng sử dụng dịch vụ.')}
        </p>
        <p>
          {t('Theo Luật Bảo vệ dữ liệu cá nhân 2025, bạn có quyền xem, chỉnh sửa, yêu cầu xóa dữ liệu và rút lại sự đồng ý — liên hệ support@vietanexpress.com. Rút lại đồng ý thì không đăng nhập bằng mã OTP qua email được nữa.')}
        </p>
        <label className={styles.remember}>
          <input type="checkbox" checked={agreed} onChange={e => { setAgreed(e.target.checked); if (e.target.checked) setConsentError(false); }} />
          <span>
            {t('Tôi đồng ý cho Việt An Express xử lý email như trên và đã đọc')}{' '}
            <button type="button" className={styles.switch} onClick={() => setTerms(true)}>{t('Điều khoản dịch vụ (mục 15)')}</button>
          </span>
        </label>
      </div>
      {consentError && (
        <p className={styles.otpError} role="alert">{t('Vui lòng đọc và đồng ý cho Việt An Express xử lý email để gửi mã đăng nhập')}</p>
      )}
      <Button variant="primary" type="submit" className={styles.full} disabled={setEmail.isPending}>
        {t(setEmail.isPending ? 'Đang gửi mã…' : 'Gửi mã xác nhận')}
      </Button>
      <p className={styles.foot}>
        <button type="button" className={styles.switch} onClick={onBack}>{t('Quay lại')}</button>
      </p>
      <TermsDialog open={terms} onClose={() => setTerms(false)} />
    </form>
  );
}
