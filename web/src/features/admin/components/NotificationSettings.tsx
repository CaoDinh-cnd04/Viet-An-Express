import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import type { NotificationAudience } from '../api';
import { summaryOf, type Draft } from '../lib/editor';
import styles from './NotificationEditor.module.css';

interface NotificationSettingsProps {
  draft: Draft;
  update: (patch: Partial<Draft>) => void;
  audience: NotificationAudience | undefined;
  /** Số khách nhận theo lựa chọn hiện tại. */
  total: number;
  /** Thông báo đang hiện / đã hẹn — không đổi giờ đăng cũ khi lưu. */
  published: boolean;
  /** Email đã gửi lần trước: không cho gửi lại. */
  lockedEmail: boolean;
}

/** Cột cài đặt bên phải trình soạn: Gửi tới · Hiển thị · Thời gian · Tóm tắt. */
export function NotificationSettings({ draft, update, audience, total, published, lockedEmail }: NotificationSettingsProps) {
  const { t } = useI18n();
  const sum = summaryOf(draft);
  return (
    <aside className={styles.settings}>
      <section className={styles.panel}>
        <h4>{t('Gửi tới')}</h4>
        <Radio checked={draft.audience === 'all'} onChange={() => update({ audience: 'all' })} title="Mọi khách"
          hint={t('Tất cả {n} khách có tài khoản portal', { n: audience?.total ?? 0 })} />
        <Radio checked={draft.audience === 'type'} onChange={() => update({ audience: 'type' })} title="Theo loại khách" hint={t('Chọn một hoặc nhiều loại')} />
        {draft.audience === 'type' && (
          <div className={styles.types}>
            {!audience?.types.length && <p className={styles.hint}>{t('Chưa có khách nào được phân loại (CustomerTypeID).')}</p>}
            {audience?.types.map(type => {
              const on = draft.customerTypes.includes(type.id);
              return (
                <label key={type.id} className={cx(styles.type, on && styles.typeOn)}>
                  <input
                    type="checkbox"
                    checked={on}
                    onChange={e => update({ customerTypes: e.target.checked ? [...draft.customerTypes, type.id] : draft.customerTypes.filter(x => x !== type.id) })}
                  />
                  {t(type.name)} <span>{type.count}</span>
                </label>
              );
            })}
          </div>
        )}
        <Radio checked={draft.audience === 'pick'} onChange={() => update({ audience: 'pick' })} title="Chọn khách" hint={t('Nhập mã từng khách')} />
        {draft.audience === 'pick' && (
          <textarea
            className={styles.codes}
            rows={3}
            value={draft.customerCodes}
            onChange={e => update({ customerCodes: e.target.value })}
            placeholder={t('Mã khách, cách nhau dấu phẩy hoặc xuống dòng')}
            aria-label={t('Mã khách')}
          />
        )}
      </section>

      <section className={styles.panel}>
        <h4>{t('Hiển thị')}</h4>
        <Toggle checked={draft.important} onChange={v => update({ important: v })} title="Quan trọng" hint="Hiện popup khi khách mở portal" />
        <Toggle checked={draft.pinned} onChange={v => update({ pinned: v })} title="Ghim lên đầu" hint="Nằm trên cùng danh sách của khách" />
        <Toggle
          checked={draft.sendEmail}
          onChange={v => update({ sendEmail: v })}
          disabled={lockedEmail}
          title="Gửi kèm email"
          hint={lockedEmail ? 'Email đã gửi, không gửi lại' : 'Gửi tới email của khách nhận (tối đa 450 địa chỉ)'}
        />
      </section>

      <section className={styles.panel}>
        <h4>{t('Thời gian')}</h4>
        <Toggle checked={draft.schedule} onChange={v => update({ schedule: v })} title="Hẹn giờ đăng" hint="Tự đăng vào lúc chọn" />
        {draft.schedule && (
          <input
            type="datetime-local"
            className={styles.input}
            value={draft.publishAt}
            onChange={e => update({ publishAt: e.target.value })}
            aria-label={t('Giờ đăng')}
          />
        )}
        <label className={styles.field}>
          {t('Hiện đến ngày')} <span>{t('· không bắt buộc')}</span>
          <input type="date" className={styles.input} value={draft.expiresOn} onChange={e => update({ expiresOn: e.target.value })} />
        </label>
      </section>

      <section className={cx(styles.panel, styles.summary)}>
        <div><span>{t('Nội dung')}</span><b>{t('{a} tin · {b} ảnh · {c} nút xem chi tiết', { a: sum.items, b: sum.images, c: sum.links + sum.details })}</b></div>
        <div><span>{t('Gửi tới')}</span><b>{t('{n} khách', { n: total })}</b></div>
        <div><span>{t('Đăng')}</span><b>{draft.schedule ? draft.publishAt.replace('T', ' ') : t(published ? 'Giữ giờ đăng' : 'Ngay')}</b></div>
        {draft.expiresOn && <div><span>{t('Hiện đến')}</span><b>{draft.expiresOn.split('-').reverse().join('/')}</b></div>}
        {draft.sendEmail && <div><span>{t('Email')}</span><b>{t(lockedEmail ? 'Đã gửi' : 'Có gửi')}</b></div>}
      </section>
    </aside>
  );
}

function Radio({ checked, onChange, title, hint }: { checked: boolean; onChange: () => void; title: string; hint: string }) {
  const { t } = useI18n();
  return (
    <label className={cx(styles.radio, checked && styles.radioOn)}>
      <input type="radio" checked={checked} onChange={onChange} />
      <span>
        <b>{t(title)}</b>
        <small>{hint}</small>
      </span>
    </label>
  );
}

function Toggle({ checked, onChange, title, hint, disabled }: { checked: boolean; onChange: (v: boolean) => void; title: string; hint: string; disabled?: boolean }) {
  const { t } = useI18n();
  return (
    <label className={cx(styles.toggleRow, disabled && styles.disabled)}>
      <span>
        <b>{t(title)}</b>
        <small>{t(hint)}</small>
      </span>
      <span className={styles.toggle}>
        <input type="checkbox" checked={checked} disabled={disabled} onChange={e => onChange(e.target.checked)} />
        <span />
      </span>
    </label>
  );
}
