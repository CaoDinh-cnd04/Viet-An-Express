import { useEffect, useState } from 'react';
import { ApiError } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { Button, useToast } from '@/shared/ui';
import { useVerifyOtp } from '../api';
import { fillOtp, OTP_LENGTH } from '../lib/otp';
import type { OtpChallenge, OtpKind } from '../types';
import { OtpInput } from './OtpInput';
import styles from './LoginForm.module.css';

interface Props {
  /** Nhân viên Việt An hay khách — chọn endpoint xác nhận mã. */
  kind: OtpKind;
  challenge: OtpChallenge;
  /** Gửi lại mã (đăng nhập lại bằng mật khẩu vừa nhập) — mã cũ mất hiệu lực. */
  onResend: () => void;
  resending: boolean;
  /** Quay về bước nhập mật khẩu (bấm "Quay lại" hoặc mã hết hạn / sai quá nhiều lần). */
  onBack: () => void;
  onVerified: () => void;
}

const emptyDigits = () => Array<string>(OTP_LENGTH).fill('');
const clock = (seconds: number) => `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, '0')}`;

/** Bước 2 đăng nhập (nhân viên Việt An / khách khi bật OTP): nhập mã OTP 6 số đã gửi email — gõ, Ctrl+V hoặc nút "Dán mã"; đủ 6 số thì tự xác nhận. */
export function OtpStep({ kind, challenge, onResend, resending, onBack, onVerified }: Props) {
  const { t } = useI18n();
  const toast = useToast();
  const verify = useVerifyOtp(kind);
  const [digits, setDigits] = useState(emptyDigits);
  const [error, setError] = useState<string>();
  const [left, setLeft] = useState(challenge.expiresInSeconds);

  // Mã mới (gửi lại) → xóa ô, đếm lại từ đầu.
  useEffect(() => {
    setDigits(emptyDigits());
    setError(undefined);
    setLeft(challenge.expiresInSeconds);
    const timer = window.setInterval(() => setLeft(s => Math.max(0, s - 1)), 1000);
    return () => window.clearInterval(timer);
  }, [challenge]);

  const submit = (code = digits.join('')) => {
    if (code.length !== OTP_LENGTH) {
      setError('Nhập đủ 6 số của mã OTP');
      return;
    }
    if (verify.isPending) return;
    setError(undefined);
    verify.mutate({ challengeId: challenge.challengeId, code }, {
      onSuccess: onVerified,
      onError: e => {
        if (e instanceof ApiError && e.code === 'OTP_EXPIRED') onBack();
        else {
          setDigits(emptyDigits());
          setError('Mã OTP không đúng — nhập lại');
        }
      }
    });
  };

  const paste = async () => {
    try {
      const text = await navigator.clipboard.readText();
      const next = fillOtp(emptyDigits(), 0, text);
      if (next.digits.includes('')) {
        toast.show('Bộ nhớ tạm không có mã 6 số — hãy sao chép mã trong email trước', 'error');
        return;
      }
      setDigits(next.digits);
      submit(next.digits.join(''));
    } catch {
      toast.show('Trình duyệt không cho đọc bộ nhớ tạm — bấm vào ô đầu rồi nhấn Ctrl+V', 'error');
    }
  };

  return (
    <form onSubmit={e => { e.preventDefault(); submit(); }} noValidate className="page-stack">
      <p className={styles.mode}>{t('Xác nhận mã OTP')}</p>
      <p className={styles.otpSent}>{t('Đã gửi mã 6 số tới email {email}.', { email: challenge.email })}</p>
      <OtpInput value={digits} onChange={setDigits} onComplete={submit} invalid={!!error} disabled={verify.isPending} />
      <div className={styles.otpMeta}>
        <span className={error ? styles.otpError : undefined} role={error ? 'alert' : undefined}>
          {error ? t(error) : left > 0 ? t('Mã hết hạn sau {time}', { time: clock(left) }) : t('Mã đã hết hạn — bấm gửi lại mã')}
        </span>
        <button type="button" className={styles.switch} onClick={() => void paste()} disabled={verify.isPending}>{t('Dán mã')}</button>
      </div>
      <Button variant="primary" type="submit" className={styles.full} disabled={verify.isPending || left === 0}>
        {t(verify.isPending ? 'Đang kiểm tra…' : 'Xác nhận')}
      </Button>
      <p className={styles.foot}>
        {t('Không nhận được mã? Xem thư mục Spam hoặc')}{' '}
        <button type="button" className={styles.switch} disabled={resending} onClick={onResend}>{t('gửi lại mã')}</button>
        {' · '}
        <button type="button" className={styles.switch} onClick={onBack}>{t('Quay lại')}</button>
      </p>
    </form>
  );
}
