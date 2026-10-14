import { get, useFieldArray, useFormContext, useWatch } from 'react-hook-form';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Card, FormGrid, Icon, SelectField, TextField } from '@/shared/ui';
import { CURRENCIES, DUTY_TERMS, EXPORT_TYPES, RULES } from '../constants';
import { useFieldBinder } from '../hooks/useFieldBinder';
import { invoiceTotal, lineTotal } from '../lib/shipment';
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
  const { control, register, formState, trigger } = useFormContext<CreateOrderValues>();
  const { fields, append, remove } = useFieldArray({ control, name: 'invoice.items' });
  const [type, items = [], currency, shippingFee] = useWatch({ control, name: ['shipment.type', 'invoice.items', 'invoice.currency', 'invoice.shippingFee'] });
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
                {fields.map((f, i) => (
                  <tr key={f.id}>
                    <td className={styles.rowNo}>{i + 1}</td>
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
                      <Button size="sm" iconOnly variant="ghost" aria-label={t('Xóa mặt hàng {n}', { n: i + 1 })} disabled={fields.length <= 1} onClick={() => remove(i)}>
                        <Icon name="close" size={15} />
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <div className={styles.tableFooter}>
            <Button size="sm" onClick={() => append(emptyInvoiceItem())}>
              <Icon name="plus" size={15} /> {t('Thêm mặt hàng')}
            </Button>
          </div>
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
