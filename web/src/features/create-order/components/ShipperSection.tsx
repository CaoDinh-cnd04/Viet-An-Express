import { useEffect, useState } from 'react';
import { useFormContext, useFormState, useWatch } from 'react-hook-form';
import { AssignManagerButton, useSession } from '@/features/auth';
import { useSearchParams } from 'react-router-dom';
import { BRANCHES } from '@/shared/config/domain';
import { useI18n } from '@/shared/i18n';
import { sanitizePhone } from '@/shared/lib/phone';
import { useDebouncedCallback } from '@/shared/lib/useDebouncedCallback';
import { cx } from '@/shared/lib/cx';
import { Button, Card, FormGrid, Icon, LinkButton, SelectField, TextField } from '@/shared/ui';
import { useRecentSenders } from '../api';
import { RULES } from '@/features/order-form';
import { useCombobox } from '../hooks/useCombobox';
import { useFieldBinder } from '../hooks/useFieldBinder';
import { recentSenderQuery, recentSenderSubtitle, senderFields, type RecentSender } from '../lib/recent-senders';
import { shipperFromProfile } from '../lib/shipper-profile';
import { SectionTitle } from './SectionTitle';
import { SuggestionList } from './SuggestionList';
import type { CreateOrderValues } from '@/features/order-form';
import styles from './form.module.css';

export function ShipperSection() {
  const bind = useFieldBinder();
  const { t } = useI18n();
  const { setValue, getValues, control } = useFormContext<CreateOrderValues>();
  const shipper = useWatch({ control, name: 'shipper' });
  const address = shipper?.address ?? '';
  // Thu gọn / mở rộng: mặc định thu gọn khi đã đủ thông tin bắt buộc (thường là điền sẵn từ hồ sơ khách).
  const [open, setOpen] = useState<boolean | null>(null);
  const complete = (['company', 'contact', 'tel', 'address', 'country', 'branch'] as const).every(k => (shipper?.[k] ?? '').trim());
  const expanded = open ?? !complete;
  // Bấm Tạo đơn mà người gửi còn lỗi → tự mở ra để khách sửa.
  const { errors, submitCount } = useFormState({ control, name: 'shipper' });
  const hasErrors = !!errors.shipper;
  useEffect(() => {
    if (hasErrors) setOpen(true);
  }, [hasErrors, submitCount]);
  // Ô điện thoại: bỏ chữ cái / ký tự lạ ngay khi gõ hoặc dán.
  const keepPhone = (name: 'shipper.tel', value: string) => {
    const clean = sanitizePhone(value);
    if (clean !== value) setValue(name, clean, { shouldDirty: true });
  };

  // Điền sẵn từ hồ sơ khách. Tài khoản chính: tên công ty chỉ đọc (theo hồ sơ); tài khoản con (nhân viên) sửa được — gửi hộ người khác.
  // Khách mới (nhân viên Việt An bấm "Tạo khách mới"): hồ sơ trống, mọi ô sửa được — lưu đơn thì backend ghi người gửi vào hồ sơ khách.
  // Địa chỉ lấy hàng mọi tài khoản sửa được (05/11/2026) — lấy hàng ở địa chỉ khác hồ sơ; chỉ lưu vào đơn, hồ sơ khách không đổi.
  const session = useSession();
  const profile = session.data?.status === 'authenticated' ? session.data.user : null;
  const [params] = useSearchParams();
  const newCustomer = profile?.newCustomer === true;
  const lockCompany = profile?.accountType !== 'staff' && !params.has('from') && !newCustomer;

  // Tài khoản con: gõ tên công ty / người gửi → gợi ý người gửi đã dùng ở đơn trước; chọn → điền lại cả người gửi.
  const [senderQuery, setSenderQuery] = useState<string | null>(null);
  const scheduleSender = useDebouncedCallback((value: string | null) => setSenderQuery(value), 250);
  const recentSenders = useRecentSenders(lockCompany ? null : senderQuery);
  const senderBox = useCombobox(!lockCompany && senderQuery ? recentSenders.data ?? [] : [], (s: RecentSender) => {
    scheduleSender.cancel();
    setSenderQuery(null);
    for (const [key, value] of senderFields(s, RULES.shipperAddressMax))
      setValue(`shipper.${key}`, value, { shouldDirty: true, shouldValidate: true });
  });
  useEffect(() => {
    if (!profile) return;
    const fill = shipperFromProfile(getValues('shipper'), profile);
    for (const [key, value] of Object.entries(fill) as Array<[keyof typeof fill, string]>) setValue(`shipper.${key}`, value);
  }, [profile, getValues, setValue]);

  return (
    <Card accent
      className={cx(styles.shipperCard, styles.cleanCard)}
      title={<SectionTitle plain icon="user" title="Thông tin người gửi" />}
      actions={
        <>
          {/* Chi nhánh cùng hàng tiêu đề (nhãn bên trái ô) + mũi tên thu gọn / mở các ô người gửi */}
          <SelectField className={styles.shipperBranch} label="Chi nhánh" required options={BRANCHES} placeholder="Chọn" {...bind('shipper.branch')} />
          <Button size="sm" variant="ghost" className={styles.shipperToggle} aria-expanded={expanded} aria-controls="shipper-fields" aria-label={t(expanded ? 'Thu gọn' : 'Mở rộng')} title={t(expanded ? 'Thu gọn' : 'Mở rộng')} onClick={() => setOpen(!expanded)}>
            <Icon name="chevronDown" size={14} className={cx(styles.chevron, expanded && styles.chevronOpen)} />
          </Button>
        </>
      }
    >
      {!expanded && (
        <button type="button" className={styles.collapsedSummary} onClick={() => setOpen(true)} title={t('Bấm để sửa thông tin người gửi')}>
          <span className={styles.summaryMain}>
            <strong>{shipper?.company}</strong>
            {shipper?.originalShipper?.trim() && <span> · {t('Shipper gốc')}: {shipper.originalShipper}</span>}
          </span>
          <span className={styles.summarySub}>
            {[shipper?.contact, shipper?.tel, shipper?.email].filter(v => v?.trim()).join(' · ')}
          </span>
          <span className={styles.summarySub}>{shipper?.address}</span>
          <span className={styles.summaryEdit}>{t('Chỉnh sửa')}</span>
        </button>
      )}
      {/* Ô nhập luôn giữ trong form (chỉ ẩn) để giá trị và kiểm tra lỗi không bị mất khi thu gọn. */}
      {/* 2 ô 1 hàng: tên · shipper gốc / liên hệ · điện thoại / địa chỉ lấy hàng (cả hàng) / MST · email / quốc gia · link sửa hồ sơ. */}
      <div id="shipper-fields" hidden={!expanded}>
      {newCustomer && (
        <p className={styles.newCustomerHint}>{t('Khách mới: thông tin người gửi sẽ lưu vào hồ sơ khách khi lưu nháp / in, CS hoàn thiện sau ở CMS.')}</p>
      )}
      <FormGrid>
        <div className={styles.suggestCell}>
          <TextField
            label="Tên công ty / người gửi"
            required
            readOnly={lockCompany}
            {...(lockCompany ? {} : senderBox.inputProps)}
            {...bind('shipper.company', lockCompany ? undefined : {
              onChange: () => {
                scheduleSender(recentSenderQuery(getValues('shipper.company')));
                senderBox.onType();
              },
              onBlur: senderBox.close
            })}
          />
          {senderBox.visible.length > 0 && (
            <SuggestionList
              id={senderBox.listId}
              label="Người gửi đã dùng"
              items={senderBox.visible.map((s, i) => ({ key: `${i}-${s.company}`, title: s.company, subtitle: recentSenderSubtitle(s) }))}
              active={senderBox.active}
              onPick={senderBox.pick}
            />
          )}
        </div>
        <TextField
          label="Tên shipper gốc"
          placeholder="Khi forwarder gửi hộ"
          maxLength={RULES.originalShipperMax}
          {...bind('shipper.originalShipper')}
        />
        <TextField label="Người liên hệ" required {...bind('shipper.contact')} />
        <TextField label="Điện thoại" required type="tel" {...bind('shipper.tel', { onChange: e => keepPhone('shipper.tel', e.target.value) })} />
        <TextField
          label="Địa chỉ lấy hàng"
          required
          wide
          maxLength={RULES.shipperAddressMax}
          aside={`${address.length}/${RULES.shipperAddressMax}`}
          {...bind('shipper.address')}
        />
        <TextField label="Mã số thuế / CCCD / CMND" {...bind('shipper.taxId')} />
        <TextField label="Email" type="email" {...bind('shipper.email')} />
        <TextField label="Quốc gia gửi" required {...bind('shipper.country')} />
        {/* Hồ sơ người gửi (tên công ty, địa chỉ) do CS sửa: gửi yêu cầu hỗ trợ — cùng hàng với quốc gia, không thêm dòng */}
        {profile?.accountType !== 'staff' && !newCustomer && (
          <div className={styles.profileLink}>
            <LinkButton to="/troubles?new=sender-info" size="sm" variant="ghost">{t('Sửa hồ sơ người gửi')}</LinkButton>
          </div>
        )}
        {/* Khách mới: góc dưới (chỗ "Sửa hồ sơ người gửi") là nút gán / đổi nhân viên phụ trách — chỉ quản trị / CS thấy */}
        {newCustomer && (
          <div className={styles.profileLink}>
            <AssignManagerButton />
          </div>
        )}
      </FormGrid>
      </div>
    </Card>
  );
}
