import { Fragment, useEffect } from 'react';
import { get, useFieldArray, useFormContext, useWatch } from 'react-hook-form';
import { isPerPackageInvoice } from '@/shared/config/domain';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Card, FormGrid, Icon, Notice, SelectField, TextField } from '@/shared/ui';
import { CURRENCIES, DUTY_TERMS, EXPORT_TYPES, RULES } from '../constants';
import { useFieldBinder } from '../hooks/useFieldBinder';
import { invoiceTotal, lineTotal } from '../lib/shipment';
import { copyPreviousPackage, packageCount, packageGroups, syncPackageItems } from '../lib/package-invoice';
import { emptyInvoiceItem, type CreateOrderValues } from '../schema';
import { UnitCell } from './UnitCell';
import { decimalField, onCellArrows } from '../hooks/cellNavigation';
import styles from './form.module.css';

type ItemKey = keyof ReturnType<typeof emptyInvoiceItem>;

/** Ô nhiều dòng tự giãn theo nội dung (trình duyệt chưa hỗ trợ field-sizing thì chỉnh chiều cao khi gõ). */
const autoGrow = (e: React.FormEvent<HTMLTextAreaElement>) => {
  const el = e.currentTarget;
  el.style.height = 'auto';
  el.style.height = `${el.scrollHeight + 2}px`;
};

export function InvoiceSection() {
  const bind = useFieldBinder();
  const { t } = useI18n();
  const { control, register, formState, trigger, getValues } = useFormContext<CreateOrderValues>();
  const { fields, append, remove, replace } = useFieldArray({ control, name: 'invoice.items' });
  const [type, items = [], currency, shippingFee] = useWatch({ control, name: ['shipment.type', 'invoice.items', 'invoice.currency', 'invoice.shippingFee'] });
  const [hub, packages = []] = useWatch({ control, name: ['service.hub', 'packages'] });
  // Tuyến khai invoice riêng từng kiện (Chuyên tuyến Úc / EU / Canada…): số invoice = tổng số kiện ở chi tiết kiện hàng.
  const perPackage = type !== 'DOC' && isPerPackageInvoice(hub);
  const count = packageCount(packages);
  useEffect(() => {
    const next = syncPackageItems(getValues('invoice.items') ?? [], perPackage, count, emptyInvoiceItem);
    if (next) replace(next);
  }, [perPackage, count, getValues, replace]);
  const rootError = get(formState.errors, 'invoice.items.root')?.message as string | undefined;
  const exportType = useWatch({ control, name: 'invoice.exportType' });
  const exportTypes = [...EXPORT_TYPES, ...(exportType && !EXPORT_TYPES.some(o => o.value === exportType) ? [{ value: exportType, label: exportType }] : [])];
  const currencies = CURRENCIES.some(c => c === currency) || !currency ? CURRENCIES : [...CURRENCIES, currency];
  const fmt = new Intl.NumberFormat('vi-VN', { maximumFractionDigits: 2 });
  const err = (i: number, key: ItemKey) => get(formState.errors, `invoice.items.${i}.${key}`)?.message as string | undefined;
  // numeric: chỉ nhận số; maxDecimals: số chữ số sau dấu chấm tối đa (đơn giá: 2).
  const cell = (i: number, key: ItemKey, props: Record<string, unknown> = {}, numeric = false, maxDecimals?: number) => (
    <input
      className={cx(styles.cell, err(i, key) && styles.cellInvalid)}
      title={err(i, key)}
      {...props}
      {...(numeric
        // Số lượng: báo lỗi ngay khi gõ (số nguyên với ĐVT có sẵn), không đợi rời ô.
        ? decimalField(register(`invoice.items.${i}.${key}`, key === 'qty' ? { onChange: () => void trigger(`invoice.items.${i}.qty`) } : undefined), false, maxDecimals)
        : register(`invoice.items.${i}.${key}`))}
    />
  );
  // Tên / nhà sản xuất: Enter để xuống dòng (vd "Nhà sản xuất" ↵ "Địa chỉ"); dòng dài tự xuống dòng.
  const area = (i: number, key: ItemKey, props: Record<string, unknown> = {}) => (
    <textarea
      rows={1}
      className={cx(styles.cell, styles.cellArea, err(i, key) && styles.cellInvalid)}
      title={err(i, key)}
      aria-invalid={err(i, key) ? true : undefined}
      onInput={autoGrow}
      {...props}
      {...register(`invoice.items.${i}.${key}`)}
    />
  );

  // 1 dòng mặt hàng: `no` = số thứ tự hiển thị (trong kiện khi khai riêng từng kiện); `removable` = còn dòng khác thì cho xóa.
  const row = (i: number, no: number, removable: boolean) => (
    <tr key={fields[i]?.id ?? i}>
      <td className={styles.rowNo}>{no}</td>
      <td>
        <div className={styles.stack}>
          {area(i, 'descEn', { placeholder: t('Tên tiếng Anh (bắt buộc)'), 'aria-label': t('Tên EN mặt hàng {n}', { n: i + 1 }) })}
          {area(i, 'descVi', { placeholder: t('Tên tiếng Việt (bắt buộc)'), 'aria-label': t('Tên VN mặt hàng {n}', { n: i + 1 }) })}
          {area(i, 'manufacturer', { placeholder: t('Nhà sản xuất ↵ địa chỉ (Enter để xuống dòng)'), 'aria-label': t('Nhà sản xuất mặt hàng {n}', { n: i + 1 }) })}
        </div>
      </td>
      <td>{cell(i, 'origin', { 'aria-label': t('Xuất xứ mặt hàng {n}', { n: i + 1 }) })}</td>
      <td>{cell(i, 'hs', { placeholder: '6204.43', 'aria-label': t('Mã HS mặt hàng {n}', { n: i + 1 }) })}</td>
      <td>
        <div className={styles.qtyUnit}>
          {cell(i, 'qty', { 'aria-label': t('Số lượng mặt hàng {n}', { n: i + 1 }) }, true)}
          <UnitCell index={i} error={err(i, 'unit')} />
        </div>
      </td>
      <td>{cell(i, 'price', { 'aria-label': t('Đơn giá mặt hàng {n}', { n: i + 1 }) }, true, RULES.priceDecimals)}</td>
      <td className={styles.computed}>{items[i] ? fmt.format(lineTotal(items[i])) : 0}</td>
      <td>
        <Button size="sm" iconOnly variant="ghost" aria-label={t('Xóa mặt hàng {n}', { n: i + 1 })} disabled={!removable} onClick={() => remove(i)}>
          <Icon name="close" size={15} />
        </Button>
      </td>
    </tr>
  );

  return (
    <Card accent title="Invoice" subtitle="(khai báo hải quan)">
      {type === 'DOC' ? (
        <p className={styles.hint}>{t('Đơn chứng từ (DOC) không cần khai invoice. Chỉ khai shipping fee bên dưới nếu có.')}</p>
      ) : (
        <>
          {/* Thư viện mặt hàng / Invoice cũ / Nhập từ Excel: ở khung "Hỗ trợ khách hàng" cột trái (tab Khai invoice) */}
          <FormGrid columns={3}>
            <SelectField label="Hình thức xuất khẩu" required placeholder="Chọn hình thức xuất khẩu" options={exportTypes} {...bind('invoice.exportType')} />
            <SelectField
              label="Hình thức chịu thuế"
              required
              placeholder="Chọn hình thức chịu thuế"
              options={DUTY_TERMS}
              {...bind('invoice.dutyTerms')}
            />
            <SelectField label="Đơn vị tiền tệ" required placeholder="Đơn vị tiền tệ" options={currencies} {...bind('invoice.currency')} />
          </FormGrid>

          {perPackage && (
            <p className={styles.notice}>
              {t('Tuyến này khai invoice riêng từng kiện: {count} kiện = {count} invoice. Số invoice tự đổi theo số kiện ở "Chi tiết kiện hàng".', { count })}
            </p>
          )}
          <div className={cx(styles.tableScroll, styles.spaced)}>
            <table className={cx(styles.table, styles.invoiceTable)} onKeyDown={onCellArrows}>
              <thead>
                <tr>
                  <th className={styles.colNo}>#</th>
                  <th>{t('Mô tả hàng hóa')} <span className={styles.required}>*</span> — {t('tên EN / VN / nhà sản xuất')}</th>
                  <th className={styles.colNum}>{t('Xuất xứ')}</th>
                  <th className={styles.colHs}>{t('Mã HS')}</th>
                  <th className={styles.colQtyUnit}>{t('SL')} <span className={styles.required}>*</span> / {t('ĐVT')}</th>
                  <th className={styles.colNum}>{t('Đơn giá')} <span className={styles.required}>*</span></th>
                  <th className={cx(styles.colNum, styles.amountHeader)}>{t('Thành tiền')}</th>
                  <th className={styles.colDel}><span className="visually-hidden">{t('Xóa')}</span></th>
                </tr>
              </thead>
              <tbody>
                {perPackage
                  ? packageGroups(items, count).map(g => (
                    <Fragment key={`pkg-${g.pkg}`}>
                      <tr className={styles.packageHead}>
                        <td colSpan={8}>
                          <div className={styles.packageHeadRow}>
                            <b>{g.extra ? t('Kiện {n} (đã bớt)', { n: g.pkg }) : t('Kiện {n}/{count}', { n: g.pkg, count })}</b>
                            <span className={styles.packageSubtotal}>
                              {fmt.format(g.indices.reduce((s, i) => s + (items[i] ? lineTotal(items[i]) : 0), 0))} {currency}
                            </span>
                            {!g.extra && g.pkg > 1 && (
                              <Button size="sm" variant="ghost" onClick={() => replace(copyPreviousPackage(getValues('invoice.items') ?? [], g.pkg))}>
                                <Icon name="copy" size={14} /> {t('Chép từ kiện trước')}
                              </Button>
                            )}
                          </div>
                        </td>
                      </tr>
                      {g.indices.map((i, n) => row(i, n + 1, g.extra || g.indices.length > 1))}
                      {!g.extra && (
                        <tr className={styles.packageFoot}>
                          <td colSpan={8}>
                            <Button size="sm" onClick={() => append({ ...emptyInvoiceItem(), package: String(g.pkg) })}>
                              <Icon name="plus" size={15} /> {t('Thêm mặt hàng')}
                            </Button>
                          </td>
                        </tr>
                      )}
                    </Fragment>
                  ))
                  : fields.map((_, i) => row(i, i + 1, fields.length > 1))}
              </tbody>
            </table>
          </div>
          {rootError && (
            <div className={styles.spaced}>
              <Notice tone="danger" title={t('Invoice từng kiện chưa đủ')}>{t(rootError)}</Notice>
            </div>
          )}

          {!perPackage && (
            <div className={styles.tableFooter}>
              <Button size="sm" onClick={() => append(emptyInvoiceItem())}>
                <Icon name="plus" size={15} /> {t('Thêm mặt hàng')}
              </Button>
            </div>
          )}
        </>
      )}

      {/* Shipping fee và tổng giá trị invoice trên cùng 1 hàng */}
      <div className={cx(styles.spaced, styles.feeRow)}>
        <TextField label="Shipping fee (nếu khai)" type="number" min={0} step="any" suffix={currency} {...bind('invoice.shippingFee')} />
        {type !== 'DOC' && (
          <div className={styles.invoiceTotal}>
            <span>{t('Tổng giá trị invoice · {n} mặt hàng', { n: items.length })}</span>
            <strong>{fmt.format(invoiceTotal(items, shippingFee))} {currency}</strong>
          </div>
        )}
      </div>
    </Card>
  );
}
