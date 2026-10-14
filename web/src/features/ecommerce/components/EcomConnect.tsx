import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { useCopyToClipboard } from '@/shared/lib/useCopyToClipboard';
import { Button, Card, Icon, Modal, Notice, SelectField, StatusPill, TextField, useToast } from '@/shared/ui';
import {
  useDisconnectStore,
  useEcomSettings,
  useRegenerateApiKey,
  useSaveEcomSettings,
  useStartStoreConnection,
  useStoreConnections,
  useSyncStore,
  useTestWebhook
} from '../api';
import { API_EXAMPLE, STORE_PLATFORMS, STORE_STATUS, TIKTOK_REGIONS, WEBHOOK_EVENTS } from '../constants';
import { formatSyncTime, needsReauthorize, readOAuthResult } from '../lib/store-connection';
import type { EcomSettings, StoreConnection, StorePlatform, TiktokRegion } from '../types';
import styles from './ecommerce.module.css';

/** Tab "Kết nối": sàn TikTok Shop (OAuth) và API cho lập trình viên. */
export function EcomConnect() {
  return (
    <div className="page-stack">
      <StoresCard />
      <DeveloperApi />
    </div>
  );
}

function StoresCard() {
  const { t } = useI18n();
  const toast = useToast();
  const [params, setParams] = useSearchParams();
  const stores = useStoreConnections();
  const [disconnecting, setDisconnecting] = useState<StoreConnection | null>(null);
  const unavailable = stores.isError;

  // Sàn redirect về backend, backend chuyển tiếp về ?tab=connect&connected=… hoặc &error=…
  useEffect(() => {
    const result = readOAuthResult(params);
    if (!result) return;
    if (result.ok) toast.show(t('Đã kết nối {platform}', { platform: STORE_PLATFORMS[result.platform as StorePlatform]?.label ?? result.platform }), 'success');
    else toast.show(result.error, 'error');
    setParams({ tab: 'connect' }, { replace: true });
  }, [params, setParams, t, toast]);

  return (
    <Card title="Kết nối sàn" subtitle="· đơn mới tự về tab Đơn hàng">
      <div className="page-stack">
        {unavailable && (
          <Notice tone="warning">
            {t(
              isNotImplemented(stores.error)
                ? 'Máy chủ chưa có chức năng kết nối sàn. Phần này sẽ hoạt động khi backend hoàn tất.'
                : 'Không tải được danh sách cửa hàng, vui lòng thử lại sau.'
            )}
          </Notice>
        )}
        {stores.data?.map(s => <StoreRow key={s.id} store={s} onDisconnect={() => setDisconnecting(s)} />)}
        <TiktokConnect disabled={unavailable} />
      </div>
      <DisconnectModal store={disconnecting} onClose={() => setDisconnecting(null)} />
    </Card>
  );
}

function TiktokConnect({ disabled }: { disabled: boolean }) {
  const { t } = useI18n();
  const start = useStartStoreConnection();
  const [region, setRegion] = useState<TiktokRegion>('global');

  return (
    <div className={styles.connectRow}>
      <strong className={styles.connectName}>TikTok Shop</strong>
      <SelectField
        label="Thị trường"
        options={TIKTOK_REGIONS.map(r => ({ value: r.value, label: t(r.label) }))}
        value={region}
        onChange={e => setRegion(e.target.value as TiktokRegion)}
        disabled={disabled}
      />
      <Button variant="primary" disabled={disabled || start.isPending} onClick={() => start.mutate({ platform: 'tiktok', region })}>
        <Icon name="link" size={15} /> {t('Kết nối')}
      </Button>
    </div>
  );
}

function StoreRow({ store, onDisconnect }: { store: StoreConnection; onDisconnect: () => void }) {
  const { t } = useI18n();
  const sync = useSyncStore();
  const start = useStartStoreConnection();
  const status = STORE_STATUS[store.status];

  return (
    <div className={styles.storeRow}>
      <div className={styles.storeHead}>
        <div>
          <strong>{store.shopName}</strong>{' '}
          <span className={styles.sub}>
            {STORE_PLATFORMS[store.platform].label}
            {' · '}
            {store.lastSyncAt ? t('đồng bộ {date}', { date: formatSyncTime(store.lastSyncAt) }) : t('chưa đồng bộ')}
          </span>
        </div>
        <div className={styles.formActions}>
          <StatusPill tone={status.tone}>{status.label}</StatusPill>
          {needsReauthorize(store) ? (
            <Button size="sm" variant="primary" disabled={start.isPending} onClick={() => start.mutate({ platform: store.platform, shopDomain: store.shopDomain })}>
              {t('Ủy quyền lại')}
            </Button>
          ) : (
            <Button size="sm" disabled={sync.isPending} onClick={() => sync.mutate(store.id)}>
              <Icon name="refresh" size={15} /> {t('Đồng bộ')}
            </Button>
          )}
          <Button size="sm" iconOnly aria-label={t('Ngắt kết nối {shop}', { shop: store.shopName })} onClick={onDisconnect}><Icon name="close" size={15} /></Button>
        </div>
      </div>
      {store.lastError && <Notice tone="danger">{store.lastError}</Notice>}
    </div>
  );
}

function DisconnectModal({ store, onClose }: { store: StoreConnection | null; onClose: () => void }) {
  const { t } = useI18n();
  const disconnect = useDisconnectStore();
  return (
    <Modal
      open={!!store}
      title="Ngắt kết nối cửa hàng"
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>{t('Hủy')}</Button>
          <Button variant="primary" disabled={disconnect.isPending} onClick={() => store && disconnect.mutate(store.id, { onSuccess: onClose })}>
            {t('Ngắt kết nối')}
          </Button>
        </>
      }
    >
      <p>{t('Việt An sẽ ngừng nhận đơn từ {shop} và gỡ ứng dụng khỏi shop. Đơn đã nhận vẫn giữ nguyên.', { shop: store?.shopName ?? '' })}</p>
    </Modal>
  );
}

/** Thu gọn mặc định — chỉ shop có lập trình viên mới cần. */
function DeveloperApi() {
  const { t } = useI18n();
  const settings = useEcomSettings();
  const save = useSaveEcomSettings();
  const regenerate = useRegenerateApiKey();
  const testWebhook = useTestWebhook();
  const copy = useCopyToClipboard();
  const [webhookUrl, setWebhookUrl] = useState('');
  const [events, setEvents] = useState<EcomSettings['webhookEvents']>([]);
  const data = settings.data;
  const unavailable = settings.isError;

  useEffect(() => {
    if (!data) return;
    setWebhookUrl(data.webhookUrl);
    setEvents(data.webhookEvents);
  }, [data]);

  return (
    <details className={cx(styles.details, styles.devApi)}>
      <summary>
        <Icon name="key" size={16} /> {t('Dành cho lập trình viên: API key, webhook')}
      </summary>
      <div className="page-stack">
        {unavailable && (
          <Notice tone="warning">
            {t(isNotImplemented(settings.error) ? 'Máy chủ chưa có chức năng cấu hình API. Phần này sẽ hoạt động khi backend hoàn tất.' : 'Không tải được cấu hình, vui lòng thử lại sau.')}
          </Notice>
        )}

        <div>
          <h3 className={styles.devTitle}>{t('API key')}</h3>
          <div className={styles.keyList}>
            {(data?.apiKeys ?? []).map(k => (
              <div key={k.env} className={styles.keyRow}>
                <span className={styles.keyEnv}>{k.env === 'production' ? 'Production' : 'Sandbox'}</span>
                <code className={styles.key}>{k.key}</code>
                <Button size="sm" iconOnly aria-label={t('Sao chép key {env}', { env: k.env })} onClick={() => void copy(k.key, t('Đã sao chép API key'))}><Icon name="copy" size={15} /></Button>
                <Button size="sm" onClick={() => regenerate.mutate(k.env)} disabled={regenerate.isPending}><Icon name="refresh" size={15} /> {t('Tạo lại')}</Button>
              </div>
            ))}
            {!data && <p className={styles.muted}>{t(settings.isLoading ? 'Đang tải…' : 'Chưa có API key.')}</p>}
          </div>
          <p className={styles.hint}>{t('Tạo lại key sẽ vô hiệu key cũ ngay lập tức. Dùng key Sandbox để thử nghiệm, không tạo đơn thật.')}</p>
        </div>

        <div>
          <h3 className={styles.devTitle}>{t('Tạo đơn qua API')}</h3>
          <div className={styles.codeWrap}>
            <button type="button" className={styles.codeCopy} onClick={() => void copy(API_EXAMPLE, t('Đã sao chép ví dụ cURL'))}>{t('Sao chép')}</button>
            <pre className={styles.code}>{API_EXAMPLE}</pre>
          </div>
        </div>

        <div className="page-stack">
          <h3 className={styles.devTitle}>{t('Webhook trạng thái đơn')}</h3>
          <TextField label="URL nhận webhook" type="url" placeholder="https://shop.com/webhook/vietan" value={webhookUrl} onChange={e => setWebhookUrl(e.target.value)} disabled={unavailable} />
          <fieldset className={styles.events} disabled={unavailable}>
            <legend>{t('Sự kiện gửi')}</legend>
            {WEBHOOK_EVENTS.map(ev => (
              <label key={ev}>
                <input type="checkbox" checked={events.includes(ev)} onChange={() => setEvents(list => (list.includes(ev) ? list.filter(x => x !== ev) : [...list, ev]))} />
                <code>{ev}</code>
              </label>
            ))}
          </fieldset>
          <div className={styles.formActions}>
            <Button onClick={() => testWebhook.mutate()} disabled={unavailable || !webhookUrl || testWebhook.isPending}><Icon name="send" size={15} /> {t('Gửi thử')}</Button>
            <Button variant="primary" onClick={() => save.mutate({ webhookUrl, webhookEvents: events })} disabled={unavailable || save.isPending}>{t('Lưu webhook')}</Button>
          </div>
        </div>
      </div>
    </details>
  );
}
