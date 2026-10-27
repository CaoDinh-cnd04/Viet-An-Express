import { useEffect, useRef, useState } from 'react';
import { useParams } from 'react-router-dom';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { fetchPrintHtml } from '../api';
import { parsePrintJob, printJobKey, type PrintJob } from '../lib/print-job';

/** Chờ lệnh in tối đa (cấp bill chậm / storage bị chặn) rồi báo lỗi. */
const WAIT_MS = 2 * 60 * 1000;

/**
 * Tab in /print/:job (mở bằng noopener từ usePrintDocuments): đợi lệnh in trong localStorage,
 * tải trang in từ backend rồi thay toàn bộ tài liệu — trang in tự mở hộp thoại in.
 */
export default function PrintPage() {
  const { job = '' } = useParams();
  const { t } = useI18n();
  const [message, setMessage] = useState(t('Đang chuẩn bị bản in…'));
  const started = useRef(false);

  useEffect(() => {
    document.title = t('Đang chuẩn bị bản in…');
    const key = printJobKey(job);

    const run = (j: PrintJob) => {
      if (started.current) return;
      started.current = true;
      cleanup();
      try {
        window.localStorage.removeItem(key);
      } catch {
        /* storage bị chặn */
      }
      if ('cancel' in j) {
        window.close();
        return;
      }
      fetchPrintHtml(j.bills, j.doc)
        .then(html => {
          document.open();
          document.write(html);
          document.close();
        })
        .catch(e => setMessage(getErrorMessage(e)));
    };

    const read = () => {
      try {
        const j = parsePrintJob(window.localStorage.getItem(key));
        if (j) run(j);
      } catch {
        /* storage bị chặn */
      }
    };
    const onStorage = (e: StorageEvent) => {
      if (e.key === key) read();
    };
    const timer = window.setTimeout(() => {
      cleanup();
      if (!started.current) setMessage(t('Không nhận được lệnh in — hãy đóng tab này và bấm in lại'));
    }, WAIT_MS);
    function cleanup() {
      window.removeEventListener('storage', onStorage);
      window.clearTimeout(timer);
    }

    window.addEventListener('storage', onStorage);
    read();
    return cleanup;
  }, [job, t]);

  return <p style={{ font: '14px system-ui, sans-serif', padding: 24, color: '#4d6456' }}>{message}</p>;
}
