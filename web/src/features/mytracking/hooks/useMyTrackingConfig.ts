import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { getErrorMessage } from '@/shared/api/http';
import { myTrackingApi, type Publication } from '../api';
import { emptyConfig, myTrackingSchema, type MyTrackingConfig } from '../schema';
import { ApiConfigRepository } from '../repositories/ApiConfigRepository';
import type { ConfigRepository } from '../repositories/ConfigRepository';
const serverRepository = new ApiConfigRepository(myTrackingApi);
/** Cấu hình MyTracking lưu trên server, có link công khai theo mã khách. */
export function useMyTrackingConfig(userId: string, server: ConfigRepository = serverRepository) {
  const form = useForm<MyTrackingConfig>({ resolver: zodResolver(myTrackingSchema), defaultValues: emptyConfig(), mode: 'onBlur' });
  const [publication, setPublication] = useState<Publication>({});
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [feedback, setFeedback] = useState<{ message: string; warning: boolean }>();
  const { reset } = form;
  useEffect(() => {
    let active = true;
    setLoading(true);
    server.load(userId).then(result => {
      if (!active) return;
      reset(result.config);
      if (result.publication) setPublication(result.publication);
      setFeedback(result.warning ? { message: result.warning, warning: true } : undefined);
    }).catch(() => {
      if (active) setFeedback({ message: 'Không tải được cấu hình. Bạn vẫn có thể chỉnh sửa bản mới.', warning: true });
    }).finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [server, userId, reset]);
  const persist = async (values: MyTrackingConfig) => {
    setSaving(true);
    try {
      const result = await server.save(userId, values);
      reset(values);
      if (result.publication) setPublication(result.publication);
      setFeedback({ message: result.warning ?? 'Đã lưu cấu hình MyTracking', warning: !result.persistent });
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
  return { form, config: form.watch(), publication, loading, saving, feedback, save, restoreDefaults };
}
