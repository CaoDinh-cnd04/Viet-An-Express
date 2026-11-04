import { useEffect, useState, type ReactNode } from 'react';
import { Controller, get, useFieldArray, useFormContext, useWatch } from 'react-hook-form';
import { Button, Card, Icon, Notice, useToast } from '@/shared/ui';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { getErrorMessage } from '@/shared/api/http';
import { isPerPackageInvoice } from '@/shared/config/domain';
import { useCategories, useFavoriteCategory, type Category } from '../api';
import { MULTI_CATEGORY, PACKAGE_ITEM_DESCRIPTION, RULES, invoicePackageCount, packageChargeableWeight, summarizePackages, volumetricWeight, emptyPackage, type CreateOrderValues } from '@/features/order-form';
import { PACKAGING_TYPES } from '../constants';
import { SectionTitle } from './SectionTitle';
import { evaluatePackages } from '../lib/carrier-limits';
import { weightRule } from '@/shared/lib/shipping-weight';
import { CategoryPicker } from './CategoryPicker';
import { decimalField, onCellArrows } from '../hooks/cellNavigation';
import styles from './form.module.css';

const NUM_COLS = [
  { key: 'length', label: 'Dài (cm)' },
  { key: 'width', label: 'Rộng (cm)' },
  { key: 'height', label: 'Cao (cm)' },
  { key: 'weight', label: 'Cân/kiện (kg)', required: true }
] as const;

/**
 * Chi tiết kiện hàng: mỗi dòng gồm SL, bao bì, nhóm hàng, mô tả mặt hàng (chỉ khi "Nhiều loại hàng"), kích thước, cân;
 * tự tính quy đổi & cân tính cước.
 */
/** `children`: phần đặt cuối khung (dòng "Dịch vụ thêm"). */
export function PackagesTable({ onPackagesInput, children }: { onPackagesInput: () => void; children?: ReactNode }) {
  const { t } = useI18n();
  const { control, register, formState, setValue } = useFormContext<CreateOrderValues>();
  const { fields, append, remove } = useFieldArray({ control, name: 'packages' });
  const packages = useWatch({ control, name: 'packages' }) ?? [];
  const carrier = useWatch({ control, name: 'service.carrier' });
  const hub = useWatch({ control, name: 'service.hub' });
  const service = { carrier, hub };
  const declaredPieces = useWatch({ control, name: 'shipment.pieces' });
  const isPack = useWatch({ control, name: 'shipment.type' }) !== 'DOC';
  // Tuyến khai invoice riêng từng kiện (Chuyên tuyến Úc / EU / Canada…): nhắc ngay khi khai từ 2 kiện.
  const perPackageInvoice = isPack && isPerPackageInvoice(hub);
  const invoiceCount = invoicePackageCount(declaredPieces, packages);
  const summary = summarizePackages(packages, service);
  // Công thức cân (trước đây là các dòng chữ xám dưới bảng) — gom vào dấu ? cạnh tiêu đề "Tính cước", rê chuột mới hiện.
  const rule = weightRule(service);
  const formula = [
    t('Quy đổi = D×R×C ÷ {n}', { n: RULES.volumetricDivisor }),
    t(rule.label),
    t('Quy đổi và tính cước ở mỗi dòng là tổng theo SL.'),
    rule.dimension === 'up' ? t('Kích thước tính quy đổi làm tròn lên cm.') : rule.dimension === 'nearest' ? t('Kích thước tính quy đổi làm tròn đến cm gần nhất.') : ''
  ].filter(Boolean).join(' · ');
  const warnings = evaluatePackages(carrier, packages);
  const err = (path: string) => get(formState.errors, path)?.message as string | undefined;
  // Số kiện của đơn (shipment.pieces) = tổng SL các dòng kiện — không còn ô nhập riêng; lệch (vd nháp cũ) thì tự sửa theo bảng.
  useEffect(() => {
    if (summary.pieces > 0 && String(summary.pieces) !== String(declaredPieces ?? '')) setValue('shipment.pieces', String(summary.pieces), { shouldDirty: true });
  }, [summary.pieces, declaredPieces, setValue]);
  const qtyMismatch = err('packages.root');
  const reg = (path: `packages.${number}.${'qty' | 'packaging' | 'length' | 'width' | 'height' | 'weight'}`) =>
    register(path, { onChange: onPackagesInput });
  const { data: categories = [] } = useCategories();
  const favorite = useFavoriteCategory();
  const toast = useToast();
  // Ngôi sao trong ô chọn nhóm: nhóm yêu thích (cả nhóm chung) hiện ở mục "Nhóm yêu thích" đầu danh sách.
  const toggleFavorite = (c: Category) =>
    favorite.mutate({ id: c.id, isFavorite: !c.isFavorite }, { onError: e => toast.show(getErrorMessage(e), 'error') });
  // Bong bóng nhắc (như hướng dẫn) dưới "Tổng kiện" khi số kiện ≥ 2 — hiện sau khi khách gõ xong,
  // "Đã hiểu" thì ẩn tới khi số kiện đổi lại. Khung Invoice vẫn có ghi chú.
  const [hintFor, setHintFor] = useState(0);
  const [dismissedFor, setDismissedFor] = useState(0);
  const needHint = perPackageInvoice && invoiceCount > 1;
  useEffect(() => {
    const timer = window.setTimeout(() => setHintFor(needHint ? invoiceCount : 0), 600);
    return () => window.clearTimeout(timer);
  }, [needHint, invoiceCount]);
  const showHint = needHint && hintFor === invoiceCount && dismissedFor !== invoiceCount;

  return (
    <Card accent className={styles.cleanCard} title={<SectionTitle icon="box" title="Chi tiết kiện hàng" sub="Kích thước tính bằng cm · Khối lượng tính bằng kg" />}>
      {qtyMismatch && (
        <Notice tone="danger" title={t('Số lượng kiện chưa khớp')}>
          {t(qtyMismatch)}
        </Notice>
      )}
      <div className={cx(styles.tableScroll, styles.tableClean, qtyMismatch && styles.spaced)}>
        {/* ← → chuyển ô khi con trỏ ở đầu / cuối ô */}
        <table className={cx(styles.table, styles.pkgTable)} onKeyDown={onCellArrows}>
          <thead>
            <tr>
              <th className={styles.colIdx}>#</th>
              <th className={styles.colQty}>{t('SL')} <span className={styles.required}>*</span></th>
              <th className={styles.colPackaging}>{t('Loại bao bì')} <span className={styles.required}>*</span></th>
              <th>{t('Nhóm hàng hóa')} <span className={styles.required}>*</span></th>
              {PACKAGE_ITEM_DESCRIPTION && <th>{t('Mô tả mặt hàng')}</th>}
              {NUM_COLS.map(c => (
                <th key={c.key} className={cx(styles.colNum, c.key === 'weight' && styles.colWide)}>
                  {t(c.label)} {'required' in c && <span className={styles.required}>*</span>}
                </th>
              ))}
              <th className={cx(styles.colNum, styles.colWide, styles.computedHeader)}>{t('Quy đổi (kg)')}</th>
              <th className={cx(styles.colNum, styles.colWide, styles.computedHeader)}>
                {t('Tính cước (kg)')}{' '}
                <span className={styles.infoTip} tabIndex={0} title={formula} aria-label={formula}><Icon name="help" size={12} /></span>
              </th>
              <th className={styles.colDel}><span className="visually-hidden">{t('Xóa')}</span></th>
            </tr>
          </thead>
          <tbody>
            {fields.map((f, i) => (
              <tr key={f.id}>
                <td className={styles.rowIdx}>{String(i + 1).padStart(2, '0')}</td>
                <td>
                  <input
                    aria-label={t('Số lượng dòng {n}', { n: i + 1 })}
                    aria-invalid={err(`packages.${i}.qty`) || qtyMismatch ? true : undefined}
                    className={cx(styles.cell, (err(`packages.${i}.qty`) || qtyMismatch) && styles.cellInvalid)}
                    {...decimalField(reg(`packages.${i}.qty`), true)}
                  />
                </td>
                <td>
                  <select aria-label={t('Bao bì dòng {n}', { n: i + 1 })} className={cx(styles.cell, err(`packages.${i}.packaging`) && styles.cellInvalid)} {...reg(`packages.${i}.packaging`)}>
                    <option value="">{t('Chọn')}</option>
                    {PACKAGING_TYPES.map(p => <option key={p} value={p}>{t(p)}</option>)}
                    {packages[i]?.packaging && !PACKAGING_TYPES.some(p => p === packages[i]!.packaging) && <option value={packages[i]!.packaging}>{t(packages[i]!.packaging)}</option>}
                  </select>
                </td>
                <td>
                  <Controller
                    control={control}
                    name={`packages.${i}.category`}
                    render={({ field }) => (
                      <CategoryPicker
                        value={field.value ?? ''}
                        onChange={field.onChange}
                        onBlur={field.onBlur}
                        categories={categories}
                        onToggleFavorite={toggleFavorite}
                        invalid={!!err(`packages.${i}.category`)}
                        ariaLabel={t('Nhóm hàng dòng {n}', { n: i + 1 })}
                        title={err(`packages.${i}.category`) ? t(err(`packages.${i}.category`)!) : undefined}
                      />
                    )}
                  />
                </td>
                {/* Mô tả mặt hàng: tạm ẩn (PACKAGE_ITEM_DESCRIPTION) */}
                {PACKAGE_ITEM_DESCRIPTION && (
                  <td>
                    {packages[i]?.category === MULTI_CATEGORY ? (
                      <input
                        aria-label={t('Mô tả mặt hàng dòng {n}', { n: i + 1 })}
                        aria-invalid={err(`packages.${i}.description`) ? true : undefined}
                        placeholder={t('VD: Quần áo, mỹ phẩm, đồ gia dụng')}
                        className={cx(styles.cell, err(`packages.${i}.description`) && styles.cellInvalid)}
                        {...register(`packages.${i}.description`)}
                      />
                    ) : (
                      <span className={styles.cellMuted} title={t('Chỉ nhập khi chọn "Nhiều loại hàng"')}>—</span>
                    )}
                  </td>
                )}
                {NUM_COLS.map(c => (
                  <td key={c.key}>
                    <input
                      aria-label={t('{col} dòng {n}', { col: t(c.label), n: i + 1 })}
                      aria-invalid={err(`packages.${i}.${c.key}`) ? true : undefined}
                      className={cx(styles.cell, err(`packages.${i}.${c.key}`) && styles.cellInvalid)}
                      {...decimalField(reg(`packages.${i}.${c.key}`))}
                    />
                  </td>
                ))}
                <td><span className={styles.readBox}>{packages[i] ? volumetricWeight(packages[i], service) : 0}</span></td>
                <td><span className={styles.readBox}>{packages[i] ? packageChargeableWeight(packages[i], service) : 0}</span></td>
                <td>
                  <Button
                    size="sm"
                    iconOnly
                    variant="ghost"
                    aria-label={t('Xóa dòng kiện {n}', { n: i + 1 })}
                    disabled={fields.length <= 1}
                    onClick={() => {
                      remove(i);
                      queueMicrotask(onPackagesInput);
                    }}
                  >
                    <Icon name="trash" size={15} />
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className={styles.tableFooter}>
        <Button
          size="sm"
          onClick={() => {
            append(emptyPackage());
            queueMicrotask(onPackagesInput);
          }}
        >
          <Icon name="plus" size={15} /> {t('Thêm kiện')}
        </Button>
        <dl className={styles.totals}>
          {/* Bong bóng nhắc invoice từng kiện bám "Tổng kiện" */}
          <div className={styles.piecesAnchor}>
            <dt>{t('Tổng kiện')}</dt>
            <dd>{summary.pieces}</dd>
            {showHint && (
              <span className={styles.callout} role="status">
                <span>{t('Tuyến này khai invoice riêng từng kiện: {count} kiện = {count} invoice (Kiện 1/{count} … {count}/{count}) ở bước Invoice.', { count: invoiceCount })}</span>
                <Button size="sm" variant="primary" onClick={() => setDismissedFor(invoiceCount)}>{t('Đã hiểu')}</Button>
              </span>
            )}
          </div>
          <div><dt>{t('Cân thực')}</dt><dd>{summary.grossWeight} kg</dd></div>
          <div><dt>{t('Quy đổi')}</dt><dd>{summary.volumetricWeight} kg</dd></div>
          <div><dt>{t('Tính cước')}</dt><dd className={styles.emphasis}>{summary.chargeableWeight} kg</dd></div>
        </dl>
      </div>
      {warnings.length > 0 && (
        <div className={styles.warnings}>
          {warnings.map(w => (
            <Notice key={w.title} tone={w.level === 'critical' ? 'danger' : w.level === 'warning' ? 'warning' : 'info'} title={w.title}>
              {w.detail}
            </Notice>
          ))}
        </div>
      )}
      {children}
    </Card>
  );
}
