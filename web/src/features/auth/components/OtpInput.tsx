import { useRef, type ClipboardEvent, type KeyboardEvent } from 'react';
import { useI18n } from '@/shared/i18n';
import { fillOtp, OTP_LENGTH } from '../lib/otp';
import styles from './LoginForm.module.css';

interface Props {
  /** 6 ô, mỗi phần tử 1 số ('' = ô trống) — giữ đúng vị trí khi ô giữa còn trống. */
  value: readonly string[];
  onChange: (digits: string[]) => void;
  /** Điền đủ 6 số (gõ hoặc dán) → xác nhận luôn. */
  onComplete?: (code: string) => void;
  invalid?: boolean;
  disabled?: boolean;
}

/**
 * Ô nhập mã OTP: 6 ô, mỗi ô 1 số — gõ tự sang ô sau, Backspace về ô trước, mũi tên trái / phải để di chuyển.
 * Dán (Ctrl+V) ở ô nào cũng điền đủ mã; điện thoại tự gợi ý mã từ email/SMS (autocomplete one-time-code).
 */
export function OtpInput({ value, onChange, onComplete, invalid, disabled }: Props) {
  const { t } = useI18n();
  const refs = useRef<(HTMLInputElement | null)[]>([]);
  const digits = Array.from({ length: OTP_LENGTH }, (_, i) => value[i] ?? '');
  const clear = (index: number) => onChange(digits.map((d, i) => (i === index ? '' : d)));

  const apply = (index: number, text: string) => {
    const next = fillOtp(digits, index, text);
    onChange(next.digits);
    refs.current[next.focus]?.focus();
    if (!next.digits.includes('')) onComplete?.(next.digits.join(''));
  };

  const onKeyDown = (index: number, e: KeyboardEvent<HTMLInputElement>) => {
    if (e.key === 'Backspace' && !digits[index] && index > 0) {
      e.preventDefault();
      clear(index - 1);
      refs.current[index - 1]?.focus();
    } else if (e.key === 'ArrowLeft' && index > 0) refs.current[index - 1]?.focus();
    else if (e.key === 'ArrowRight' && index < OTP_LENGTH - 1) refs.current[index + 1]?.focus();
  };

  const onPaste = (index: number, e: ClipboardEvent<HTMLInputElement>) => {
    e.preventDefault();
    apply(index, e.clipboardData.getData('text'));
  };

  return (
    <div className={styles.otpBoxes} role="group" aria-label={t('Mã OTP')}>
      {digits.map((d, i) => (
        <input
          key={i}
          ref={el => { refs.current[i] = el; }}
          className={styles.otpBox}
          value={d}
          inputMode="numeric"
          autoComplete={i === 0 ? 'one-time-code' : 'off'}
          autoFocus={i === 0}
          aria-label={t('Số thứ {n} của mã OTP', { n: i + 1 })}
          aria-invalid={invalid || undefined}
          disabled={disabled}
          onFocus={e => e.target.select()}
          onKeyDown={e => onKeyDown(i, e)}
          onPaste={e => onPaste(i, e)}
          // Ô đã có số mà gõ thêm: lấy số mới gõ. Điện thoại tự điền cả mã vào ô đầu → điền đủ 6 ô.
          onChange={e => (e.target.value ? apply(i, e.target.value.replace(d, '') || e.target.value) : clear(i))}
        />
      ))}
    </div>
  );
}
