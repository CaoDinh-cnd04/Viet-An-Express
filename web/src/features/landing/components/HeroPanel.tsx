import { useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { LoginForm } from '@/features/auth';
import { useI18n } from '@/shared/i18n';
import { Button, Icon, SegmentedControl, TextAreaField } from '@/shared/ui';
import { MAX_TRACK_BILLS, parseBills } from '../lib/tracking';
import styles from './HeroPanel.module.css';

type Tab = 'track' | 'login';

interface HeroPanelProps {
  /** Mã đang tra (từ `?track=`) — điền sẵn vào ô nhập. */
  initialBills: readonly string[];
  onTrack: (bills: string[]) => void;
  tracking: boolean;
}

/** Khung "Tra cứu & đăng nhập" ở đầu trang. Tab đồng bộ với URL: `/` = tra cứu, `/login` = đăng nhập. */
export function HeroPanel({ initialBills, onTrack, tracking }: HeroPanelProps) {
  const { t } = useI18n();
  const { pathname, search } = useLocation();
  const navigate = useNavigate();
  const tab: Tab = pathname === '/login' ? 'login' : 'track';

  return (
    <div className={styles.panel} id="tra-cuu">
      <SegmentedControl<Tab>
        ariaLabel={t('Chọn chức năng')}
        value={tab}
        onChange={next => void navigate({ pathname: next === 'login' ? '/login' : '/', search }, { replace: true })}
        options={[
          { value: 'track', label: 'Tra cứu vận đơn' },
          { value: 'login', label: 'Đăng nhập portal' }
        ]}
      />
      {tab === 'track' ? (
        <TrackForm initialBills={initialBills} onTrack={onTrack} tracking={tracking} />
      ) : (
        <LoginForm autoFocus />
      )}
    </div>
  );
}

function TrackForm({ initialBills, onTrack, tracking }: HeroPanelProps) {
  const { t } = useI18n();
  const [text, setText] = useState(() => initialBills.join('\n'));
  const [error, setError] = useState<string>();
  const [warning, setWarning] = useState<string>();

  const submit = (e: FormEvent) => {
    e.preventDefault();
    const { bills, invalid, dropped } = parseBills(text);
    if (bills.length === 0) {
      setWarning(undefined);
      return setError(invalid.length > 0 ? t('Số vận đơn không hợp lệ: {bills}', { bills: invalid.join(', ') }) : t('Nhập ít nhất 1 số vận đơn'));
    }
    setError(undefined);
    const notes = [
      invalid.length > 0 && t('Bỏ qua mã sai định dạng: {bills}', { bills: invalid.join(', ') }),
      dropped > 0 && t('Chỉ tra {max} vận đơn đầu tiên, bỏ qua {n} số', { max: MAX_TRACK_BILLS, n: dropped })
    ].filter(Boolean);
    setWarning(notes.length > 0 ? notes.join('. ') : undefined);
    onTrack(bills);
  };

  return (
    <form onSubmit={submit} noValidate className="page-stack">
      <TextAreaField
        label="Số vận đơn"
        rows={4}
        placeholder={t('Mỗi dòng 1 số vận đơn\nVD: 6156979')}
        hint={warning ?? t('Tối đa {max} số mỗi lần, cách nhau bằng xuống dòng hoặc dấu phẩy', { max: MAX_TRACK_BILLS })}
        error={error}
        value={text}
        onChange={e => setText(e.target.value)}
        onKeyDown={e => {
          // Ctrl/Cmd + Enter để tra nhanh.
          if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) e.currentTarget.form?.requestSubmit();
        }}
      />
      <Button variant="primary" type="submit" className={styles.submit} disabled={tracking}>
        <Icon name="search" size={16} />
        {t(tracking ? 'Đang tra cứu…' : 'Theo dõi vận đơn')}
      </Button>
    </form>
  );
}

/**
 * Ô tra cứu dạng thanh ngang ở giữa hero: gõ 1 hoặc nhiều số vận đơn (cách nhau dấu phẩy / khoảng trắng) rồi Enter.
 * Cùng quy tắc đọc mã với khung tra cứu nhiều dòng (parseBills).
 */
export function TrackBar({ onTrack }: { onTrack: (bills: string[]) => void }) {
  const { t } = useI18n();
  const [text, setText] = useState('');
  const [note, setNote] = useState<{ tone: 'error' | 'warning'; text: string }>();

  const submit = (e: FormEvent) => {
    e.preventDefault();
    const { bills, invalid, dropped } = parseBills(text);
    if (bills.length === 0) {
      return setNote({ tone: 'error', text: invalid.length > 0 ? t('Số vận đơn không hợp lệ: {bills}', { bills: invalid.join(', ') }) : t('Nhập ít nhất 1 số vận đơn') });
    }
    const notes = [
      invalid.length > 0 && t('Bỏ qua mã sai định dạng: {bills}', { bills: invalid.join(', ') }),
      dropped > 0 && t('Chỉ tra {max} vận đơn đầu tiên, bỏ qua {n} số', { max: MAX_TRACK_BILLS, n: dropped })
    ].filter(Boolean);
    setNote(notes.length > 0 ? { tone: 'warning', text: notes.join('. ') } : undefined);
    onTrack(bills);
  };

  return (
    <form className={styles.bar} onSubmit={submit} noValidate role="search" id="tra-cuu">
      <label className={styles.barField}>
        <Icon name="search" size={18} />
        <input
          value={text}
          onChange={e => {
            setText(e.target.value);
            setNote(undefined);
          }}
          placeholder={t('Nhập số vận đơn (nhiều số cách nhau dấu phẩy)…')}
          aria-label={t('Số vận đơn')}
          aria-invalid={note?.tone === 'error' || undefined}
          enterKeyHint="search"
        />
      </label>
      <Button variant="primary" type="submit" className={styles.barButton}>
        {t('Tra cứu')}
      </Button>
      {note && <p className={note.tone === 'error' ? styles.barError : styles.barWarning} role="alert">{note.text}</p>}
    </form>
  );
}
