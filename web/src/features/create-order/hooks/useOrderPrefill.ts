import { useCallback, useEffect, useRef } from 'react';
import { useWatch, type UseFormReturn } from 'react-hook-form';
import { useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { CARRIER_HUBS, CARRIERS, defaultHub } from '@/shared/config/domain';
import { useDebouncedCallback } from '@/shared/lib/useDebouncedCallback';
import { useToast } from '@/shared/ui';
import { useDrafts } from '@/features/drafts/api';
import { useOrderCopy } from '@/features/orders/api';
import { cleanLoadedContacts, copyOrderValues } from '../lib/copy-order';
import { defaultValues, type CreateOrderValues } from '../schema';

/**
 * Khóa bản tự lưu theo tài khoản đang đăng nhập (mã khách + tên đăng nhập) — admin đăng xuất, nhân viên đăng nhập
 * cùng tab sẽ không thấy đơn đang điền dở (kèm người gửi) của tài khoản trước.
 */
export function autosaveKey(user?: { customerCode: string; userName?: string } | null): string {
  return user ? `va.createOrder.autosave:${user.customerCode}:${user.userName ?? ''}` : 'va.createOrder.autosave';
}

/** Khóa bản tự lưu của 1 đơn nháp đang sửa — tách khỏi bản tự lưu của đơn mới. Hàm thuần — có test. */
export function draftAutosaveKey(storageKey: string, draftId: string): string {
  return `${storageKey}:draft:${draftId}`;
}

export const copyAutosaveKey = (storageKey: string, bill: string): string => `${storageKey}:copy:${bill}`;

/** Bản tự lưu trong phiên (sessionStorage) — giữ dữ liệu khi đổi chế độ Từng bước / 1 trang hoặc tải lại trang. */
export function readAutosave(key: string): CreateOrderValues | undefined {
  try {
    const raw = window.sessionStorage.getItem(key);
    // Ghép từng nhóm với mặc định — bản tự lưu cũ có thể thiếu trường mới của form (vd goods.multi).
    return raw ? copyOrderValues(JSON.parse(raw) as Partial<CreateOrderValues>) : undefined;
  } catch {
    return undefined;
  }
}

export function clearAutosave(key: string): void {
  try {
    window.sessionStorage.removeItem(key);
  } catch {
    /* storage bị chặn */
  }
}

const looksLikeForm = (v: unknown): v is CreateOrderValues =>
  !!v && typeof v === 'object' && 'shipper' in v && 'receiver' in v && 'shipment' in v;

/**
 * Nguồn dữ liệu ban đầu của form tạo đơn (ưu tiên theo thứ tự):
 * - ?draft=<id>   : mở lại đơn nháp để sửa — có thay đổi chưa lưu trong phiên (tải lại trang giữa chừng) thì khôi phục bản đó,
 *                   không thì dùng payload đã lưu.
 * - ?from=<bill>  : nhân bản từ đơn đã tạo.
 * - ?carrier=&country= : chọn dịch vụ từ trang Giá & gợi ý.
 * - không có tham số: khôi phục bản tự lưu trong phiên.
 * Trả về id đơn nháp đang sửa (nếu có) để trang gọi cập nhật thay vì tạo mới.
 */
export function useOrderPrefill(form: UseFormReturn<CreateOrderValues>, storageKey: string): {
  draftId: string | null;
  finishAutosave: (opts?: { keepEditing?: CreateOrderValues }) => void;
  retainDraft: (id: string) => void;
  copyLoading: boolean;
  copyError: unknown;
  retryCopy: () => void;
} {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const toast = useToast();
  const draftId = params.get('draft');
  // Đơn mới và từng đơn nháp có bản tự lưu riêng.
  const fromBill = params.get('from');
  const key = draftId ? draftAutosaveKey(storageKey, draftId) : fromBill ? copyAutosaveKey(storageKey, fromBill) : storageKey;
  const carrier = params.get('carrier');
  const country = params.get('country');
  const drafts = useDrafts();
  const source = useOrderCopy(draftId ? null : fromBill);
  const applied = useRef<string | null>(null);

  // Mở đơn nháp
  useEffect(() => {
    if (!draftId || applied.current === `draft:${draftId}`) return;
    const draft = drafts.data?.find(d => d.id === draftId);
    if (!draft) return;
    applied.current = `draft:${draftId}`;
    const unsaved = readAutosave(draftAutosaveKey(storageKey, draftId));
    if (unsaved) {
      form.reset(unsaved);
      toast.show('Đã khôi phục thay đổi chưa lưu của đơn nháp này.');
    // Ghép từng nhóm với giá trị mặc định: nháp đọc từ dbo.MaVanDon thiếu trường form (vd goods.multi) → không báo lỗi ô ẩn.
    } else if (looksLikeForm(draft.payload)) form.reset(cleanLoadedContacts(copyOrderValues(draft.payload)));
    else form.reset({ ...defaultValues(), receiver: { ...defaultValues().receiver, company: draft.cnee, country: draft.ct }, service: { ...defaultValues().service, reference: draft.ref } });
  }, [draftId, drafts.data, form, storageKey, toast]);

  // Nháp không còn (đã xoá hoặc đã in bill, vd xoá ở "Đơn nháp & chưa in" rồi bấm Back): bỏ ?draft khỏi URL để trang
  // về chế độ tạo đơn mới — không giữ tiêu đề "Sửa đơn nháp" và không gửi cập nhật vào nháp đã mất.
  // Chỉ kết luận khi danh sách nháp đã tải xong (không đang tải lại) — tránh báo nhầm ngay sau khi vừa lưu nháp mới.
  useEffect(() => {
    if (!draftId || applied.current === `draft:${draftId}` || !drafts.isSuccess || drafts.isFetching) return;
    if (drafts.data.some(d => d.id === draftId)) return;
    clearAutosave(draftAutosaveKey(storageKey, draftId));
    toast.show('Đơn nháp này không còn (đã xóa hoặc đã in bill) — chuyển sang tạo đơn mới.');
    navigate(pathname, { replace: true });
  }, [draftId, drafts.isSuccess, drafts.isFetching, drafts.data, storageKey, toast, navigate, pathname]);

  // Copy toàn bộ form; bản đang sửa có khóa riêng để tải lại/đổi chế độ không mất thay đổi.
  useEffect(() => {
    const appliedKey = `from:${storageKey}:${fromBill}`;
    if (draftId || !fromBill || !source.data || applied.current === appliedKey) return;
    applied.current = appliedKey;
    const saved = readAutosave(key);
    const values = saved ? copyOrderValues(saved) : cleanLoadedContacts(copyOrderValues(source.data));
    form.reset(values);
    try { window.sessionStorage.setItem(key, JSON.stringify(values)); } catch { /* storage unavailable */ }
  }, [draftId, fromBill, source.data, form, key, storageKey]);

  // Chọn dịch vụ từ trang Giá
  useEffect(() => {
    if (draftId || fromBill || !carrier || applied.current === `carrier:${carrier}`) return;
    applied.current = `carrier:${carrier}`;
    const name = CARRIERS.find(c => carrier.startsWith(c)) ?? carrier;
    // Hub tự được chọn theo hãng trong ServiceSection
    if (CARRIER_HUBS[name]) {
      form.setValue('service.carrier', name, { shouldDirty: true });
      form.setValue('service.hub', defaultHub(name), { shouldDirty: true });
    }
    if (country) form.setValue('receiver.country', country);
  }, [draftId, fromBill, carrier, country, form]);

  // Tự lưu trong phiên — cả đơn mới lẫn đơn nháp đang sửa (mỗi nháp 1 khóa), chỉ khi có thay đổi chưa lưu.
  const values = useWatch({ control: form.control });
  const stopped = useRef(false);
  const save = useDebouncedCallback((target: string, v: unknown) => {
    if (stopped.current) return;
    try {
      window.sessionStorage.setItem(target, JSON.stringify(v));
    } catch {
      /* storage bị chặn */
    }
  }, 600);
  useEffect(() => {
    if (stopped.current || !form.formState.isDirty) return;
    if (fromBill && !draftId && applied.current === `from:${storageKey}:${fromBill}`) {
      try { window.sessionStorage.setItem(key, JSON.stringify(form.getValues())); } catch { /* storage unavailable */ }
    } else save(key, values);
  }, [values, key, form.formState.isDirty, save, fromBill, draftId, storageKey, form]);

  // Đã lưu đơn: huỷ lần tự lưu còn chờ rồi xoá bản tạm. Nếu không, lần tự lưu còn chờ có thể chạy SAU khi xoá
  // (trang kế tiếp tải chậm nên form cũ vẫn còn) và lần mở "Tạo đơn" sau bị điền sẵn đơn vừa tạo.
  // keepEditing: vừa "Lưu nháp" và vẫn ở lại sửa → đánh dấu form = bản đã lưu (hết "chưa lưu") và tiếp tục tự lưu
  // các lần sửa sau; không có → rời trang, dừng hẳn.
  const finishAutosave = useCallback((opts?: { keepEditing?: CreateOrderValues }) => {
    save.cancel();
    clearAutosave(key);
    if (key !== storageKey) clearAutosave(storageKey);
    if (opts?.keepEditing) form.reset(opts.keepEditing, { keepValues: true });
    else stopped.current = true;
  }, [save, key, storageKey, form]);

  // Form hiện tại chính là bản vừa lưu; không reset từ cache khi gắn ID vào URL.
  const retainDraft = useCallback((id: string) => {
    applied.current = `draft:${id}`;
  }, []);

  return { draftId, finishAutosave, retainDraft, copyLoading: !draftId && !!fromBill && source.isPending,
    copyError: !draftId && fromBill ? source.error : null, retryCopy: () => { void source.refetch(); } };
}
