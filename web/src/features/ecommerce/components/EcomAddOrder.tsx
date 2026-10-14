import { useState } from 'react';
import { getErrorMessage, isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { readFileAsText } from '@/shared/lib/files';
import { Card, FileDrop, LinkButton, Notice, SegmentedControl } from '@/shared/ui';
import { useImportEcomCsv, useStoreConnections } from '../api';
import { ECOM_IMPORT_TEMPLATE_URL } from '../constants';
import { ManualEcomForm } from './ManualEcomForm';
import styles from './ecommerce.module.css';

const METHODS = [
  { value: 'manual', label: 'Nhập tay từng đơn' },
  { value: 'excel', label: 'Từ file Excel / CSV' }
] as const;
type Method = (typeof METHODS)[number]['value'];

/** Tab "Thêm đơn": nhập tay từng đơn (mặc định) hoặc nhập nhiều đơn bằng file. Đơn từ sàn đã kết nối tự về, không cần thêm ở đây. */
export function EcomAddOrder() {
  const { t } = useI18n();
  const [method, setMethod] = useState<Method>('manual');
  const connected = useStoreConnections().data ?? [];
  return (
    <div className="page-stack">
      <div className={styles.addHead}>
        <SegmentedControl ariaLabel="Cách thêm đơn" options={METHODS} value={method} onChange={setMethod} />
        {connected.length > 0 && (
          <p className={styles.hint}>
            {t('Đơn trên {shops} tự về tab Đơn hàng khi đồng bộ — ở đây chỉ thêm đơn bán ngoài shop đã kết nối.', { shops: connected.map(s => s.shopName).join(', ') })}
          </p>
        )}
      </div>
      {method === 'manual' ? <ManualEcomForm /> : <CsvImport />}
    </div>
  );
}

function CsvImport() {
  const { t } = useI18n();
  const importCsv = useImportEcomCsv();
  const [fileName, setFileName] = useState('');
  const [badFile, setBadFile] = useState(false);

  const onFile = async (file: File) => {
    importCsv.reset();
    setFileName(file.name);
    const isCsv = /\.csv$/i.test(file.name);
    setBadFile(!isCsv);
    if (isCsv) importCsv.mutate(await readFileAsText(file));
  };

  const result = importCsv.data;
  return (
    <Card title="Nhập đơn từ file CSV">
      <ol className={styles.steps}>
        <li>{t('Tải file mẫu CSV của Việt An:')} <a href={ECOM_IMPORT_TEMPLATE_URL} download>{t('tải file mẫu')}</a></li>
        <li>{t('Điền đơn theo đúng các cột của file mẫu, lưu dạng CSV UTF-8.')}</li>
        <li>{t('Kéo thả file .csv vào ô dưới.')}</li>
      </ol>
      <div className={styles.spaced}>
        <FileDrop
          accept=".csv,text/csv"
          disabled={importCsv.isPending}
          onFile={f => void onFile(f)}
          title={importCsv.isPending ? t('Đang xử lý {name}…', { name: fileName }) : 'Kéo & thả file CSV hoặc bấm để chọn'}
          hint="File .csv theo mẫu Việt An, tối đa 5 MB"
        />
      </div>
      {badFile && <div className={styles.spaced}><Notice tone="danger">{t('Chỉ nhận file .csv. Với file Excel, hãy lưu lại dạng CSV UTF-8.')}</Notice></div>}
      {importCsv.isError && (
        <div className={styles.spaced}>
          {isNotImplemented(importCsv.error)
            ? <Notice tone="warning">{t('Nhập đơn từ file đang được hoàn thiện ở máy chủ. Tạm thời hãy dùng "Nhập tay từng đơn".')}</Notice>
            : <Notice tone="danger">{getErrorMessage(importCsv.error)}</Notice>}
        </div>
      )}
      {result && (
        <div className={styles.spaced}>
          <Notice tone={result.errors.length ? 'warning' : 'success'} title={`${fileName}: ${t(result.message)}`}>
            <LinkButton to="/ecommerce?tab=orders" size="sm">{t('Xem tab Đơn hàng')}</LinkButton>
            {result.errors.length > 0 && (
              <ul className={styles.errorList}>
                {result.errors.map((e, i) => <li key={i}>{typeof e === 'string' ? t(e) : t('Dòng {row}: {message}', { row: e.row, message: t(e.message) })}</li>)}
              </ul>
            )}
          </Notice>
        </div>
      )}
    </Card>
  );
}
