import { useState } from 'react';
import { Controller, get, useFieldArray, useFormContext, useWatch } from 'react-hook-form';
import { Button, Card, Icon, Notice, useToast } from '@/shared/ui';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { getErrorMessage } from '@/shared/api/http';
import { useCategories, useFavoriteCategory, type Category } from '../api';
import { MULTI_CATEGORY, PACKAGING_TYPES, RULES } from '../constants';
import { evaluatePackages } from '../lib/carrier-limits';
import { packageChargeableWeight, summarizePackages, volumetricWeight } from '../lib/shipment';
import { weightRule } from '@/shared/lib/shipping-weight';
import { emptyPackage, type CreateOrderValues } from '../schema';
import { CategoryManagerDialog } from './CategoryManagerDialog';
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
export function PackagesTable({ onPackagesInput }: { onPackagesInput: () => void }) {
  const { t } = useI18n();
  const { control, register, setValue, formState } = useFormContext<CreateOrderValues>();
  const { fields, append, remove } = useFieldArray({ control, name: 'packages' });
  const packages = useWatch({ control, name: 'packages' }) ?? [];
  const carrier = useWatch({ control, name: 'service.carrier' });
  const hub = useWatch({ control, name: 'service.hub' });
  const service = { carrier, hub };
  const declaredPieces = useWatch({ control, name: 'shipment.pieces' });
  const summary = summarizePackages(packages, service);
  const warnings = evaluatePackages(carrier, packages);
  const err = (path: string) => get(formState.errors, path)?.message as string | undefined;
  // Tổng SL các dòng kiện ≠ số kiện dự kiến (báo khi bấm Tạo đơn / Tiếp tục)
  const qtyMismatch = err('packages.root');
  const piecesDiffer = String(summary.pieces) !== String(Number(declaredPieces) || '');
  const reg = (path: `packages.${number}.${'qty' | 'packaging' | 'length' | 'width' | 'height' | 'weight'}`) =>
    register(path, { onChange: onPackagesInput });
  const { data: categories = [] } = useCategories();
  const favorite = useFavoriteCategory();
  const toast = useToast();
  // Ngôi sao trong ô chọn nhóm: nhóm yêu thích (cả nhóm chung) hiện ở mục "Nhóm yêu thích" đầu danh sách.
  const toggleFavorite = (c: Category) =>
    favorite.mutate({ id: c.id, isFavorite: !c.isFavorite }, { onError: e => toast.show(getErrorMessage(e), 'error') });
  const [managing, setManaging] = useState(false);

  return (
    <Card accent
      title="Chi tiết kiện hàng"
      actions={
        <div className={styles.pkgHeader}>
          <label className={styles.piecesField}>
            <span>{t('Tổng số lượng dự kiến')} <span className={styles.required}>*</span></span>
            <input
              type="number"
              min={1}
              step={1}
              aria-invalid={err('shipment.pieces') ? true : undefined}
              title={err('shipment.pieces') ? t(err('shipment.pieces')!) : undefined}
              className={cx(styles.cell, err('shipment.pieces') && styles.cellInvalid)}
              {...register('shipment.pieces')}
            />
          </label>
          <button type="button" className={styles.linkButton} onClick={() => setManaging(true)}>
            <Icon name="edit" size={13} /> {t('Thêm / sửa nhóm hàng')}
          </button>
          <span className={styles.headerHint}>{t('Quy đổi = D×R×C ÷ {n}', { n: RULES.volumetricDivisor })}</span>
        </div>
      }
    >
      {qtyMismatch && (
        <Notice tone="danger" title={t('Số lượng kiện chưa khớp')}>
          {t(qtyMismatch)}
        </Notice>
      )}
      <div className={cx(styles.tableScroll, qtyMismatch && styles.spaced)}>
        {/* ← → chuyển ô khi con trỏ ở đầu / cuối ô */}
        <table className={cx(styles.table, styles.pkgTable)} onKeyDown={onCellArrows}>
          <thead>
            <tr>
              <th className={styles.colQty}>{t('SL')} <span className={styles.required}>*</span></th>
              <th className={styles.colPackaging}>{t('Loại bao bì')} <span className={styles.required}>*</span></th>
              <th>{t('Nhóm hàng hóa')} <span className={styles.required}>*</span></th>
              <th>{t('Mô tả mặt hàng')}</th>
              {NUM_COLS.map(c => (
                <th key={c.key} className={styles.colNum}>
                  {t(c.label)} {'required' in c && <span className={styles.required}>*</span>}
                </th>
              ))}
              <th className={cx(styles.colNum, styles.computedHeader)}>{t('Quy đổi (kg)')}</th>
              <th className={cx(styles.colNum, styles.computedHeader)}>{t('Tính cước (kg)')}</th>
              <th className={styles.colDel}><span className="visually-hidden">{t('Xóa')}</span></th>
            </tr>
          </thead>
          <tbody>
            {fields.map((f, i) => (
              <tr key={f.id}>
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
                <td className={styles.computed}>{packages[i] ? volumetricWeight(packages[i], service) : 0}</td>
                <td className={styles.computed}>{packages[i] ? packageChargeableWeight(packages[i], service) : 0}</td>
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
                    <Icon name="close" size={15} />
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
          <div>
            <dt>{t('Tổng kiện')}</dt>
            <dd className={piecesDiffer ? styles.mismatch : undefined} title={piecesDiffer ? t('Chưa bằng số kiện dự kiến') : undefined}>
              {summary.pieces}
              {piecesDiffer && declaredPieces ? ` / ${declaredPieces}` : ''}
            </dd>
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
      <p className={styles.weightRuleHint}>{t(weightRule(service).label)}</p>
      <p className={styles.weightRuleHint}>{t('Quy đổi và tính cước ở mỗi dòng là tổng theo SL.')}{weightRule(service).dimension === 'up' ? ` ${t('Kích thước tính quy đổi làm tròn lên cm.')}` : weightRule(service).dimension === 'nearest' ? ` ${t('Kích thước tính quy đổi làm tròn đến cm gần nhất.')}` : ''}</p>
      <CategoryManagerDialog
        open={managing}
        onClose={() => setManaging(false)}
        onRenamed={(from, to) =>
          packages.forEach((p, i) => p.category === from && setValue(`packages.${i}.category`, to, { shouldDirty: true }))
        }
      />
    </Card>
  );
}
