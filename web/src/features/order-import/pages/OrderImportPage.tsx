import { useState } from 'react';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { BRANCHES, CARRIERS, defaultHub, hubOptions } from '@/shared/config/domain';
import { Button, Card, DataTable, FileDrop, FormGrid, Icon, LinkButton, Notice, PageHeader, SelectField, StatusPill, type Column } from '@/shared/ui';
import { useCommitImport, usePreviewImport } from '../api';
import { IMPORT_GUIDE, IMPORT_LIMITS, IMPORT_STEPS, IMPORT_TEMPLATE_URL } from '../constants';
import { formatMoney } from '@/shared/lib/format';
import { checkImportFile, formatKg, rowState, serviceHubError, sortRows } from '../lib/import-rows';
import type { ImportDefaults, ImportResult, ImportRow } from '../types';
import styles from './OrderImportPage.module.css';

const TEMPLATE_NAME = 'Mau_Excel_Tao_Don.xlsx';

export default function OrderImportPage() {
  const { t } = useI18n();
  const [defaults, setDefaults] = useState<ImportDefaults>({ service: '', hub: '', branch: 'TP.HCM' });
  const [file, setFile] = useState<File | null>(null);
  const [fileError, setFileError] = useState('');
  const [result, setResult] = useState<ImportResult | null>(null);

  const preview = usePreviewImport();
  const commit = useCommitImport();
  const hubError = serviceHubError(defaults);
  const created = (result?.created ?? 0) > 0;

  const check = (f: File) => {
    commit.reset();
    setResult(null);
    preview.mutate({ file: f, defaults }, { onSuccess: res => setResult(res.data) });
  };

  const onFile = (f: File) => {
    setFile(f);
    setResult(null);
    preview.reset();
    commit.reset();
    const error = checkImportFile(f);
    setFileError(error ?? '');
    if (!error) check(f);
  };

  const create = () => {
    if (!file || hubError) return;
    commit.mutate({ file, defaults }, { onSuccess: res => setResult(res.data) });
  };

  const setService = (service: string) => setDefaults(d => ({ ...d, service, hub: defaultHub(service) }));

  const columns: ReadonlyArray<Column<ImportRow>> = [
    { key: 'line', header: 'Dòng', width: 56, align: 'center', render: r => r.line },
    { key: 'ref', header: 'Ref_No', render: r => r.ref || <span className={styles.muted}>—</span> },
    { key: 'type', header: 'Loại', className: styles.nowrap, render: r => t(r.type === 'DOC' ? 'Chứng từ' : 'Hàng hoá') },
    {
      key: 'cnee',
      header: 'Người nhận',
      render: r => (
        <div className={styles.cnee}>
          <strong>{r.consignee || '—'}</strong>
          <span className={styles.muted}>{[r.city, r.countryCode].filter(Boolean).join(', ')}</span>
        </div>
      )
    },
    { key: 'pcs', header: 'Kiện', width: 56, align: 'right', render: r => r.pieces },
    { key: 'gw', header: 'Cân thực', align: 'right', className: styles.nowrap, render: r => formatKg(r.weightKg) },
    { key: 'cw', header: 'Cân tính cước', align: 'right', className: styles.nowrap, render: r => <strong>{formatKg(r.chargeableKg)}</strong> },
    { key: 'value', header: 'Giá trị', align: 'right', className: styles.nowrap, render: r => (r.value > 0 ? formatMoney(r.value, r.currency) : '—') },
    { key: 'products', header: 'Mặt hàng', width: 72, align: 'right', render: r => r.products || '—' },
    { key: 'result', header: 'Kết quả', width: '30%', render: r => <RowResult row={r} /> }
  ];

  return (
    <>
      <PageHeader
        title="Tạo đơn từ Excel"
        description="Tạo nhiều đơn cùng lúc từ file Excel mẫu: chọn dịch vụ (nếu cần) → tải file lên → kiểm tra → tạo đơn."
        actions={
          <LinkButton to={IMPORT_TEMPLATE_URL} reloadDocument download={TEMPLATE_NAME} size="sm">
            <Icon name="download" size={15} /> {t('Tải file mẫu')}
          </LinkButton>
        }
      />
      <div className="page-stack">
        <Card title="1. Dịch vụ áp cho cả file" subtitle="(không bắt buộc)">
          <FormGrid columns={3}>
            <SelectField
              label="Dịch vụ"
              placeholder="Không chọn — theo file"
              options={CARRIERS}
              value={defaults.service}
              onChange={e => setService(e.target.value)}
            />
            <SelectField
              label="Hub"
              placeholder="Chọn hub"
              required={!!defaults.service}
              disabled={!defaults.service}
              options={hubOptions(defaults.service)}
              value={defaults.hub}
              error={hubError ?? undefined}
              onChange={e => setDefaults(d => ({ ...d, hub: e.target.value }))}
            />
            <SelectField label="Chi nhánh gửi" options={BRANCHES} value={defaults.branch} onChange={e => setDefaults(d => ({ ...d, branch: e.target.value }))} />
          </FormGrid>
          <p className={styles.hint}>{t('Không bắt buộc chọn dịch vụ, nhưng nếu đã chọn dịch vụ thì cần chọn hub. Dịch vụ chọn ở đây áp cho mọi đơn trong file.')}</p>
        </Card>

        <Card title="2. Tải file lên">
          <FileDrop
            accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            onFile={onFile}
            disabled={preview.isPending || commit.isPending}
            title={file?.name ?? 'Kéo & thả file Excel hoặc bấm để chọn'}
            hint={t('File .xlsx theo mẫu · tối đa {rows} đơn / lần, {mb} MB', { rows: IMPORT_LIMITS.maxRows, mb: IMPORT_LIMITS.maxMb })}
          />
          <p className={styles.hint}>
            {t('Chưa có file?')} <a href={IMPORT_TEMPLATE_URL} download={TEMPLATE_NAME}>{t('Tải xuống file mẫu import')}</a>{' '}
            {t('— điền từ dòng 3 của sheet DATA, giữ nguyên dòng tiêu đề.')}
          </p>
          {fileError && <Notice tone="danger">{t(fileError)}</Notice>}
          {preview.isPending && <Notice tone="info">{t('Đang kiểm tra file…')}</Notice>}
          {preview.isError && <Notice tone="danger">{getErrorMessage(preview.error)}</Notice>}
        </Card>

        {result && (
          <Card
            flush
            title="3. Kiểm tra & tạo đơn"
            subtitle={
              result.invalid
                ? t('· {total} dòng: {valid} hợp lệ, {invalid} lỗi', { total: result.total, valid: result.valid, invalid: result.invalid })
                : t('· {total} dòng: {valid} hợp lệ', { total: result.total, valid: result.valid })
            }
            actions={
              <div className={styles.actions}>
                {!created && file && (
                  <Button size="sm" onClick={() => check(file)} disabled={preview.isPending || commit.isPending}>
                    <Icon name="refresh" size={15} /> {t('Kiểm tra lại')}
                  </Button>
                )}
                <Button variant="primary" size="sm" disabled={!result.valid || !!hubError || commit.isPending || created} onClick={create}>
                  {commit.isPending
                    ? t('Đang tạo đơn…')
                    : created
                      ? t('Đã tạo {n} đơn', { n: result.created })
                      : t('Tạo {n} đơn', { n: result.valid })}
                </Button>
              </div>
            }
          >
            <div className={styles.pad}>
              {created ? (
                <Notice tone="success" title={t('Đã tạo {n} đơn và cấp số vận đơn', { n: result.created })}>
                  {t('Đơn nằm trong "Đơn hàng của tôi" — in bill, invoice ngay tại đó.')}{' '}
                  <LinkButton to="/orders" size="sm" variant="ghost">{t('Xem đơn hàng')}</LinkButton>
                </Notice>
              ) : result.invalid > 0 ? (
                <Notice tone="warning">
                  {result.valid ? t('Chỉ {n} dòng hợp lệ được tạo đơn; dòng lỗi bị bỏ qua.', { n: result.valid }) : t('Chưa có dòng nào hợp lệ.')}{' '}
                  {t('Sửa lỗi trong file rồi tải lên lại.')}
                </Notice>
              ) : (
                <Notice tone="success">{t('Tất cả dòng hợp lệ. Kiểm tra cảnh báo (nếu có) rồi bấm "Tạo {n} đơn".', { n: result.valid })}</Notice>
              )}
              {commit.isError && <Notice tone="danger">{getErrorMessage(commit.error)}</Notice>}
            </div>
            <DataTable caption="Kết quả kiểm tra từng dòng" columns={columns} rows={sortRows(result.rows)} rowKey={r => String(r.line)} minWidth={1080} />
          </Card>
        )}

        <Card>
          <details className={styles.guide}>
            <summary>{t('Hướng dẫn điền file mẫu')}</summary>
            <p>
              {t('Công cụ giúp tạo nhiều đơn cùng lúc: điền file mẫu, tải lên, hệ thống kiểm tra từng dòng rồi tạo đơn cho các dòng hợp lệ.')}{' '}
              {t('Mỗi đơn là 1 dòng trong sheet DATA, dữ liệu bắt đầu từ dòng 3 (dòng 2 là chú thích).')}
            </p>
            {IMPORT_GUIDE.map(section => (
              <section key={section.title}>
                <h3>{t(section.title)}</h3>
                <table className={styles.guideTable}>
                  <thead>
                    <tr><th>{t('Cột')}</th><th>{t('Mô tả')}</th></tr>
                  </thead>
                  <tbody>
                    {section.columns.map(c => (
                      <tr key={c.name}>
                        <td><code>{c.name}</code></td>
                        <td>{t(c.description)}{c.required && <strong className={styles.required}> — {t('bắt buộc')}</strong>}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
                {section.note && <p className={styles.note}>{t(section.note)}</p>}
              </section>
            ))}
            <h3>{t('Quy trình thực hiện')}</h3>
            <ol>
              {IMPORT_STEPS.map(step => <li key={step}>{t(step)}</li>)}
            </ol>
          </details>
        </Card>
      </div>
    </>
  );
}

function RowResult({ row }: { row: ImportRow }) {
  const { t } = useI18n();
  const state = rowState(row);
  return (
    <div className={styles.result}>
      {state === 'created' && <StatusPill tone="success">Bill {row.bill}</StatusPill>}
      {state === 'ok' && <StatusPill tone="brand">{'Hợp lệ'}</StatusPill>}
      {row.errors.length > 0 && (
        <ul className={styles.errors}>
          {row.errors.map(e => <li key={e}>{t(e)}</li>)}
        </ul>
      )}
      {row.warnings.length > 0 && (
        <ul className={styles.warnings}>
          {row.warnings.map(w => <li key={w}>{t(w)}</li>)}
        </ul>
      )}
    </div>
  );
}
