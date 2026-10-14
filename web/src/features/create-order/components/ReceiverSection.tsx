import { useEffect, useMemo, useRef, useState } from 'react';
import { useFormContext, useWatch } from 'react-hook-form';
import { Button, Card, FormGrid, Icon, TextField, type TextFieldProps } from '@/shared/ui';
import { useI18n } from '@/shared/i18n';
import { useDebouncedCallback } from '@/shared/lib/useDebouncedCallback';
import { sanitizePhone } from '@/shared/lib/phone';
import { euCountryCode, isEuCountry } from '@/shared/config/eu';
import { useAddressSuggestions, useCountries, usePostalSearch, useReceiverBook, useRecentReceivers, useRemoteAreas } from '../api';
import { COUNTRIES, RULES } from '../constants';
import { useCombobox } from '../hooks/useCombobox';
import { usePostalPlaceBox } from '../hooks/usePostalPlaceBox';
import { useFieldBinder } from '../hooks/useFieldBinder';
import { splitAddressLines } from '../lib/address';
import { remoteAreaCarriers } from '../lib/remote-area';
import { receiverFields, recentReceiverQuery, recentReceiverSubtitle, type RecentReceiver } from '../lib/recent-receivers';
import { addressQuery, findCountry, normalizePostal, shouldResetAddress, suggestionFields, type AddressSuggestion } from '../lib/geo';
import { defaultValues, type CreateOrderValues } from '../schema';
import { AddressPickerDialog } from './AddressPickerDialog';
import { SuggestionList } from './SuggestionList';
import styles from './form.module.css';

const MAX = RULES.receiverAddressMax;

/** Thêm vào ô địa chỉ: xử lý gõ / rời ô và thuộc tính của ô nhập (combobox gợi ý ở Địa chỉ 1). */
interface AddressFieldExtra {
  onChange?: () => void;
  onBlur?: () => void;
  input?: Partial<TextFieldProps>;
}

export function ReceiverSection() {
  const bind = useFieldBinder();
  const { t, lang } = useI18n();
  const { control, setValue, getValues, clearErrors, setFocus, trigger } = useFormContext<CreateOrderValues>();
  const [addr1 = '', addr2 = '', addr3 = ''] = useWatch({ control, name: ['receiver.addr1', 'receiver.addr2', 'receiver.addr3'] });
  const [picking, setPicking] = useState(false);
  // Sổ địa chỉ = người nhận gần nhất lấy từ đơn cũ (tải khi mở hộp thoại).
  const book = useReceiverBook(picking);

  // ---------- Nước đến → mã điện thoại ----------
  const countries = useCountries();
  const [countryText = '', postalText = '', cityText = ''] = useWatch({ control, name: ['receiver.country', 'receiver.postal', 'receiver.city'] });
  // API lỗi → dùng danh sách tĩnh (không có mã điện thoại) để vẫn nhận ra nước, tự xoá địa chỉ khi đổi nước.
  const countryList = useMemo(
    () => (countries.data?.length ? countries.data : COUNTRIES.map(name => ({ code: name, name }))),
    [countries.data]
  );
  const country = findCountry(countryList, countryText);
  const countryCode = country?.code.length === 2 ? country.code : euCountryCode(countryText);
  const showEuTaxFields = isEuCountry(countryCode);
  useEffect(() => {
    setValue('receiver.countryCode', countryCode ?? '', { shouldDirty: true });
    if (!isEuCountry(countryCode)) {
      setValue('receiver.iossNo', '', { shouldDirty: true });
      setValue('receiver.eoriNo', '', { shouldDirty: true });
      clearErrors(['receiver.iossNo', 'receiver.eoriNo']);
    }
  }, [countryCode, setValue, clearErrors]);
  const dialCode = country?.dialCode ?? '';
  useEffect(() => {
    if (!dialCode) return; // Keep the copied prefix while the country catalogue loads or has no match.
    if ((getValues('receiver.phoneCode') ?? '') !== dialCode) setValue('receiver.phoneCode', dialCode, { shouldDirty: true });
  }, [dialCode, getValues, setValue]);

  // ---------- Mã bưu chính: khách gõ → gợi ý mã + thành phố (GeoNames), chọn → điền mã + thành phố ----------
  const [postalQuery, setPostalQuery] = useState<string | null>(null);
  const schedulePostal = useDebouncedCallback((value: string | null) => setPostalQuery(value), 400);
  useEffect(() => schedulePostal(normalizePostal(postalText)), [postalText, schedulePostal]);
  const postal = usePostalSearch(country?.code, postalQuery);
  /** Mã bưu chính vừa điền từ gợi ý địa chỉ / sổ địa chỉ (để biết đơn cũ đã có thành phố đúng). */
  const pickedPostal = useRef<string | null>(null);
  const postalBox = usePostalPlaceBox(postal.data, o => {
    const opts = { shouldDirty: true, shouldValidate: true } as const;
    setValue('receiver.postal', o.postalCode, opts);
    setValue('receiver.city', o.city, opts);
  });

  // ---------- VSVX: mã bưu chính + thành phố thuộc vùng phụ phí của hãng nào (tra khi khách ngừng gõ) ----------
  const [remoteQuery, setRemoteQuery] = useState<{ postal: string; city: string } | null>(null);
  const scheduleRemote = useDebouncedCallback((value: { postal: string; city: string } | null) => setRemoteQuery(value), 500);
  useEffect(() => {
    const p = normalizePostal(postalText);
    scheduleRemote(p ? { postal: p, city: cityText.trim() } : null);
  }, [postalText, cityText, scheduleRemote]);
  const remote = useRemoteAreas(country?.code, remoteQuery?.postal ?? null, remoteQuery?.city ?? '');
  const remoteHits = remoteQuery && normalizePostal(postalText) === remoteQuery.postal ? remote.data ?? [] : [];

  // ---------- Khách đổi sang nước khác → xoá mã bưu chính, thành phố, tỉnh / bang của nước cũ ----------
  /** Nước nhận ra gần nhất (cả khi điền bằng code: sổ địa chỉ, mở nháp) — chỉ để so sánh, không tự xoá. */
  const lastCountryCode = useRef<string | undefined>(undefined);
  useEffect(() => {
    if (country) lastCountryCode.current = country.code;
  }, [country]);

  /** Chỉ chạy khi khách tự gõ / chọn ở ô Nước đến (sự kiện nhập), không chạy khi form được điền sẵn. */
  const onCountryInput = (e: { target: { value: string } }) => {
    const next = findCountry(countryList, e.target.value)?.code;
    if (shouldResetAddress(lastCountryCode.current, next)) {
      const opts = { shouldDirty: true } as const;
      setValue('receiver.postal', '', opts);
      setValue('receiver.city', '', opts);
      setValue('receiver.state', '', opts);
      clearErrors(['receiver.postal', 'receiver.city', 'receiver.state']);
      setPostalQuery(null);
    }
    if (next) lastCountryCode.current = next;
  };

  const postalHint = country && postalQuery && !postal.isFetching && postal.data?.length === 0
    ? 'Không tìm thấy mã này — vui lòng tự nhập thành phố, tỉnh / bang'
    : undefined;

  /** Xoá toàn bộ thông tin người nhận để nhập lại từ đầu (hỏi lại khi đã có dữ liệu). */
  const clearReceiver = () => {
    const current = getValues('receiver');
    const filled = Object.entries(current).some(([k, v]) => k !== 'phoneCode' && k !== 'countryCode' && typeof v === 'string' && v.trim() !== '');
    if (filled && !window.confirm(t('Xóa toàn bộ thông tin người nhận để nhập lại?'))) return;
    setValue('receiver', defaultValues().receiver, { shouldDirty: true });
    clearErrors('receiver');
    setPostalQuery(null);
    lastCountryCode.current = undefined;
    pickedPostal.current = null;
  };

  /** Chọn 1 người nhận cũ (ô gợi ý tên công ty hoặc sổ địa chỉ) → điền lại toàn bộ thông tin người nhận. */
  const applyReceiver = (r: RecentReceiver) => {
    const opts = { shouldDirty: true, shouldValidate: true } as const;
    receiverFields(r).forEach(([field, value]) => setValue(`receiver.${field}`, value, opts));
    // Nước đổi theo người nhận đã chọn: thành phố / mã bưu chính lấy từ đơn cũ, không để tra mã ghi đè.
    lastCountryCode.current = findCountry(countryList, r.country ?? '')?.code ?? lastCountryCode.current;
    pickedPostal.current = normalizePostal(r.postalCode ?? '');
  };

  // Địa chỉ 1 / 2 quá giới hạn ký tự → tự cắt ở khoảng trắng gần nhất, phần thừa dồn xuống dòng sau (như Bill Online cũ)
  const ADDR = ['addr1', 'addr2', 'addr3'] as const;
  const onAddressInput = (name: (typeof ADDR)[number]) => {
    const current = ADDR.map(k => getValues(`receiver.${k}`) ?? '');
    const { lines, overflowFrom } = splitAddressLines(current, MAX);
    if (overflowFrom === null) return;
    lines.forEach((line, i) => line !== current[i] && setValue(`receiver.${ADDR[i]!}`, line, { shouldDirty: true, shouldValidate: true }));
    // Đang gõ ở dòng bị tràn → chuyển con trỏ xuống cuối phần vừa dồn sang dòng sau
    const from = ADDR.indexOf(name);
    if (from === overflowFrom && from < ADDR.length - 1) {
      const next = ADDR[from + 1]!;
      const moved = lines[from + 1]!.length - (current[from + 1] ?? '').trim().length - ((current[from + 1] ?? '').trim() ? 1 : 0);
      setFocus(`receiver.${next}`);
      requestAnimationFrame(() => {
        const el = document.activeElement as HTMLInputElement | null;
        el?.setSelectionRange?.(moved, moved);
      });
    }
  };
  const addressField = (name: (typeof ADDR)[number], label: string, value: string, required: boolean, extra?: AddressFieldExtra) => (
    <TextField
      label={label}
      required={required}
      wide
      // Địa chỉ 3 nhận phần dồn xuống từ địa chỉ 1 / 2: giới hạn theo cột database (250), không hiện bộ đếm.
      maxLength={name === 'addr3' ? RULES.receiverAddress3Max : undefined}
      aside={name === 'addr3' ? undefined : `${value.length}/${MAX}`}
      {...extra?.input}
      {...bind(`receiver.${name}`, { onChange: () => { onAddressInput(name); extra?.onChange?.(); }, onBlur: extra?.onBlur })}
    />
  );

  // ---------- Gợi ý địa chỉ (Geoapify) khi khách gõ Địa chỉ 1 → điền địa chỉ, thành phố, tỉnh / bang, mã bưu chính ----------
  const [addrQuery, setAddrQuery] = useState<string | null>(null);
  const scheduleAddr = useDebouncedCallback((value: string | null) => setAddrQuery(value), 350);
  const addressSuggestions = useAddressSuggestions(countryCode, addrQuery);
  const addrBox = useCombobox(addrQuery ? addressSuggestions.data ?? [] : [], (s: AddressSuggestion) => {
    scheduleAddr.cancel();
    const opts = { shouldDirty: true, shouldValidate: true } as const;
    suggestionFields(s).forEach(([field, value]) => setValue(`receiver.${field}`, value, opts));
    pickedPostal.current = normalizePostal(s.postalCode ?? '');
    setAddrQuery(null);
  });
  const addr1Extra: AddressFieldExtra = {
    onChange: () => {
      scheduleAddr(addressQuery(getValues('receiver.addr1')));
      addrBox.onType();
    },
    onBlur: addrBox.close,
    input: addrBox.inputProps
  };

  // ---------- Gõ tên công ty → người nhận đã gửi trước đây (đơn cũ của khách) → điền lại toàn bộ thông tin người nhận ----------
  const [companyQuery, setCompanyQuery] = useState<string | null>(null);
  const scheduleCompany = useDebouncedCallback((value: string | null) => setCompanyQuery(value), 250);
  const recentReceivers = useRecentReceivers(companyQuery);
  const companyBox = useCombobox(companyQuery ? recentReceivers.data ?? [] : [], (r: RecentReceiver) => {
    scheduleCompany.cancel();
    setCompanyQuery(null);
    applyReceiver(r);
  });

  return (
    <Card accent
      title="Thông tin người nhận"
      subtitle={lang === 'vi' ? '(Receiver)' : undefined}
      className={styles.fill}
      actions={
        <div className={styles.cardActions}>
          <Button size="sm" onClick={clearReceiver} title={t('Xóa toàn bộ thông tin người nhận')}>
            <Icon name="refresh" size={14} /> {t('Làm mới')}
          </Button>
          <Button size="sm" onClick={() => setPicking(true)}>{t('Sổ địa chỉ')}</Button>
        </div>
      }
    >
      {/* Thứ tự: tên công ty → liên hệ → nước / mã bưu chính / thành phố / tỉnh → địa chỉ. */}
      <FormGrid>
        <div className={styles.suggestWrap}>
          <TextField
            label="Tên công ty (company name)"
            required
            wide
            {...companyBox.inputProps}
            {...bind('receiver.company', {
              onChange: () => {
                scheduleCompany(recentReceiverQuery(getValues('receiver.company')));
                companyBox.onType();
              },
              onBlur: companyBox.close
            })}
          />
          {companyBox.visible.length > 0 && (
            <SuggestionList
              id={companyBox.listId}
              label="Người nhận đã gửi"
              items={companyBox.visible.map((r, i) => ({ key: `${i}-${r.company}`, title: r.company, subtitle: recentReceiverSubtitle(r) }))}
              active={companyBox.active}
              onPick={companyBox.pick}
            />
          )}
        </div>
        <TextField label="Người liên hệ (contact name)" required {...bind('receiver.contact')} />
        <TextField label="Điện thoại (tel)" required type="tel" prefix={dialCode || undefined} {...bind('receiver.tel', { onChange: e => { const clean = sanitizePhone(e.target.value); if (clean !== e.target.value) setValue('receiver.tel', clean, { shouldDirty: true }); } })} />
        <TextField label="Tax ID" {...bind('receiver.taxId')} />
        <TextField label="Email" type="email" {...bind('receiver.email')} />
        {showEuTaxFields && (
          <>
            <TextField label="IOSS No" placeholder="IM1234567890" {...bind('receiver.iossNo', { onBlur: () => void trigger('receiver.iossNo') })} />
            <TextField label="EORI No" placeholder="DE123456789012345" {...bind('receiver.eoriNo', { onBlur: () => void trigger('receiver.eoriNo') })} />
          </>
        )}
        <TextField label="Nước đến (country)" required list="va-countries" autoComplete="country-name" {...bind('receiver.country', { onChange: onCountryInput })} />
        <div className={styles.suggestCell}>
          <TextField label="Mã bưu chính (postal code)" hint={postalHint} {...postalBox.inputProps} {...bind('receiver.postal', { onChange: postalBox.onType, onBlur: postalBox.onBlur })} />
          {postalBox.list}
        </div>
        <TextField label="Thành phố (city)" required {...bind('receiver.city')} />
        <TextField label="Tỉnh / bang (state)" {...bind('receiver.state')} />
        <div className={styles.suggestWrap}>
          {addressField('addr1', 'Địa chỉ 1 (address 1)', addr1, true, addr1Extra)}
          {addrBox.visible.length > 0 && (
            <SuggestionList
              id={addrBox.listId}
              label="Gợi ý địa chỉ"
              items={addrBox.visible.map(s => ({ key: s.label, title: s.label }))}
              active={addrBox.active}
              onPick={addrBox.pick}
              footer="Powered by Geoapify · © OpenStreetMap contributors"
            />
          )}
        </div>
        {addressField('addr2', 'Địa chỉ 2 (address 2)', addr2, true)}
        {addressField('addr3', 'Địa chỉ 3 (address 3)', addr3, false)}
      </FormGrid>
      <datalist id="va-countries">
        {countryList.map(c => <option key={c.code} value={c.name} />)}
      </datalist>

      {remoteHits.length > 0 && remoteQuery && (
        <div className={styles.remoteArea} role="alert">
          <span>
            <strong>{t('Khu vực VSVX: {carriers}', { carriers: remoteAreaCarriers(remoteHits) })}</strong> {t('· có thể bị hãng thu phụ phí ODA')}
          </span>
        </div>
      )}
      <p className={styles.warning}>{t('Hệ thống kiểm tra VSVX chỉ mang tính chất tham khảo. Vui lòng tự kiểm tra VSVX với hãng trước khi gửi hàng.')}</p>

      <AddressPickerDialog
        open={picking}
        title="Sổ địa chỉ người nhận"
        onClose={() => setPicking(false)}
        loading={book.isLoading}
        items={(book.data ?? []).map((r, i) => ({
          key: `${i}-${r.company}`,
          title: r.company,
          subtitle: [recentReceiverSubtitle(r), r.contact].filter(Boolean).join(' · '),
          onPick: () => applyReceiver(r)
        }))}
      />
    </Card>
  );
}
