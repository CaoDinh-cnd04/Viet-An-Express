import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { getErrorMessage, isNotImplemented } from '@/shared/api/http';
import { myTrackingApi, type Publication } from '../api';
import { emptyConfig, myTrackingSchema, type MyTrackingConfig } from '../schema';
import { ApiConfigRepository } from '../repositories/ApiConfigRepository';
import type { ConfigRepository } from '../repositories/ConfigRepository';
import { LocalStorageConfigRepository } from '../repositories/LocalStorageConfigRepository';
const serverRepository = new ApiConfigRepository(myTrackingApi);
const localRepository = new LocalStorageConfigRepository();
/**
 * `server`: lưu trên server, có link công khai theo mã khách.
 * `local`: backend chưa có API (404/501) — chỉ lưu bản thử nghiệm trên trình duyệt như trước.
 */
export type StorageMode = 'server' | 'local';
export function useMyTrackingConfig(userId: string, server: ConfigRepository = serverRepository, local: ConfigRepository = localRepository) {
  const form = useForm<MyTrackingConfig>({ resolver: zodResolver(myTrackingSchema), defaultValues: emptyConfig(), mode: 'onBlur' });
  const [mode, setMode] = useState<StorageMode>('server');
  const [publication, setPublication] = useState<Publication>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [feedback, setFeedback] = useState<{ message: string; warning: boolean }>();
  const { reset } = form;
  useEffect(() => {
    let active = true;
    setLoading(true);
    const load = async () => {
      try { return { mode: 'server' as const, result: await server.load(userId) }; }
      catch (e) {
        if (!isNotImplemented(e)) throw e;
        return { mode: 'local' as const, result: await local.load(userId) };
      }
    };
    load().then(({ mode: loadedMode, result }) => {
      if (!active) return;
      setMode(loadedMode);
      reset(result.config);
      if (result.publication) setPublication(result.publication);
      setFeedback(result.warning ? { message: result.warning, warning: true } : undefined);
    }).catch(() => {
      if (active) setFeedback({ message: 'Không tải được cấu hình. Bạn vẫn có thể chỉnh sửa bản mới.', warning: true });
    }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [server, local, userId, reset]);
  const persist = async (values: MyTrackingConfig) => {
    setSaving(true);
    try {
      const result = mode === 'server' ? await server.save(userId, values) : await local.save(userId, values);
      reset(values);
      if (result.publication) setPublication(result.publication);
      const done = mode === 'local' ? 'Đã lưu bản thử nghiệm trên trình duyệt' : 'Đã lưu cấu hình MyTracking';
      setFeedback({ message: result.warning ?? done, warning: !result.persistent });
    } catch (e) {
      setFeedback({ message: getErrorMessage(e, 'Không lưu được cấu hình. Nội dung chỉnh sửa vẫn được giữ lại.'), warning: true });
    } finally { setSaving(false); }
  };
  const save = form.handleSubmit(values => persist(values));
  // Khôi phục chỉ sửa form; giữ mốc đã lưu để báo đúng thay đổi chưa lưu.
  // Tên công ty theo hồ sơ khách — khôi phục mặc định vẫn giữ.
  const restoreDefaults = () => {
    const defaults = emptyConfig();
    defaults.brand.companyName = form.getValues('brand.companyName');
    reset(defaults, { keepDefaultValues: true });
    setFeedback(undefined);
  };
  return { form, config: form.watch(), mode, publication, loading, saving, feedback, save, restoreDefaults };
}
