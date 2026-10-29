import { useEffect, useRef, useState } from 'react';
import type { FieldErrors, UseFormReturn } from 'react-hook-form';
import { useLocation, useNavigate } from 'react-router-dom';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { useToast } from '@/shared/ui';
import { onBeforeIdleLogout } from '@/features/auth';
import { useIssueAndPrint, useSaveDraft, useUpdateDraft } from '@/features/drafts';
import { buildDraftPayload, summarizePackages, toNumber, STEP_FIELDS, type CreateOrderValues } from '@/features/order-form';
import { useQuote, type ServiceQuote } from '@/features/pricing';
import { evaluatePackages, hasCritical, type PackageWarning } from '../lib/carrier-limits';
import { listFormErrors } from '../lib/form-errors';
import { PACKAGES_STEP } from '../lib/order-sections';
import { focusFirstError } from '../lib/scroll';
import type { useOrderPrefill } from './useOrderPrefill';

type Prefill = ReturnType<typeof useOrderPrefill>;

interface Options {
  form: UseFormReturn<CreateOrderValues>;
  isWizard: boolean;
  draftId: Prefill['draftId'];
  finishAutosave: Prefill['finishAutosave'];
  retainDraft: Prefill['retainDraft'];
  /** Wizard: nhảy tới bước có lỗi. */
  goToErrorStep: (step: number) => void;
}

/**
 * Luồng gửi đơn của trang tạo đơn:
 * Lưu nháp → "Đơn nháp & chưa in". Tạo đơn: kiểm tra form → chặn kiện hãng không nhận → hỏi xác nhận phụ phí / cảnh báo →
 * hỏi Điều khoản dịch vụ → đồng ý: lưu + cấp bill + in; không đồng ý: vẫn lưu (chờ in). Tự đăng xuất: lưu nháp trước.
 */
export function useOrderSubmit({ form, isWizard, draftId, finishAutosave, retainDraft, goToErrorStep }: Options) {
  const navigate = useNavigate();
  const { pathname } = useLocation();
  const toast = useToast();
  const { t } = useI18n();
  const saveDraft = useSaveDraft();
  const updateDraft = useUpdateDraft();
  const quote = useQuote();
  const issueAndPrint = useIssueAndPrint();
  const persisting = useRef(false);
  const [confirm, setConfirm] = useState<{ quote: ServiceQuote | null; warnings: PackageWarning[] } | null>(null);
  // Đơn đang chờ khách đồng ý Điều khoản dịch vụ (Tạo đơn & in bill).
  const [termsFor, setTermsFor] = useState<CreateOrderValues | null>(null);
  const saving = saveDraft.isPending || updateDraft.isPending;

  const persist = (values: CreateOrderValues, status: 'draft' | 'ready') => {
    if (persisting.current) return;
    persisting.current = true;
    const payload = buildDraftPayload(values, status);
    const onSuccess = (saved: { id: string }) => {
      finishAutosave(status === 'ready' ? undefined : { keepEditing: values });
      setConfirm(null);
      toast.show(status === 'ready' ? 'Đã tạo đơn. Bấm "In & cấp bill" để cấp mã bill và in vận đơn A4, invoice, nhãn A6.' : 'Đã lưu nháp.', 'success');
      if (status === 'ready') navigate('/drafts');
      else {
        retainDraft(saved.id);
        navigate(`${pathname}?draft=${encodeURIComponent(saved.id)}`, { replace: true });
      }
    };
    const onError = (e: unknown) => toast.show(getErrorMessage(e, 'Không lưu được đơn, vui lòng thử lại'), 'error');
    const onSettled = () => { persisting.current = false; };
    if (draftId) updateDraft.mutate({ id: draftId, draft: payload }, { onSuccess, onError, onSettled });
    else saveDraft.mutate(payload, { onSuccess, onError, onSettled });
  };

  /**
   * Khách đồng ý điều khoản: mở tab in ngay (cú bấm còn hiệu lực, không bị chặn pop-up) → lưu đơn → cấp bill → in A4 + invoice + nhãn A6,
   * rồi sang Đơn hàng của tôi. Lưu được mà cấp bill lỗi thì đơn nằm ở Đơn nháp & chưa in.
   */
  const createAndPrint = (values: CreateOrderValues) => {
    if (persisting.current) return;
    persisting.current = true;
    const payload = buildDraftPayload(values, 'ready');
    let saved = false;
    issueAndPrint.run(
      async () => {
        try {
          const res = draftId ? await updateDraft.mutateAsync({ id: draftId, draft: payload }) : await saveDraft.mutateAsync(payload);
          saved = true;
          finishAutosave(undefined);
          return res.id;
        } catch (e) {
          toast.show(getErrorMessage(e, 'Không lưu được đơn, vui lòng thử lại'), 'error');
          throw e;
        }
      },
      () => {
        persisting.current = false;
        navigate('/orders');
      },
      () => {
        persisting.current = false;
        if (saved) navigate('/drafts');
      }
    );
  };

  /** Đã qua kiểm tra (và xác nhận phụ phí nếu có): hỏi Điều khoản dịch vụ trước khi tạo & in. */
  const proceed = (values: CreateOrderValues) => {
    setConfirm(null);
    setTermsFor(values);
  };

  /** Trước khi tạo: chặn kiện hãng không nhận; hỏi xác nhận nếu có phụ phí / cảnh báo. */
  const onValid = async (values: CreateOrderValues) => {
    const isPack = values.shipment.type === 'PACK';
    const warnings = isPack ? evaluatePackages(values.service.carrier, values.packages) : [];
    if (hasCritical(warnings)) {
      if (isWizard) goToErrorStep(PACKAGES_STEP);
      toast.show('Có kiện vượt giới hạn nhận của hãng — vui lòng chia nhỏ kiện hoặc đổi dịch vụ.', 'error');
      return;
    }

    let matched: ServiceQuote | null = null;
    if (isPack) {
      const longest = Math.max(0, ...values.packages.map(p => Math.max(toNumber(p.length), toNumber(p.width), toNumber(p.height))));
      const biggest = values.packages.find(p => Math.max(toNumber(p.length), toNumber(p.width), toNumber(p.height)) === longest);
      try {
        const rates = await quote.mutateAsync({
          country: values.receiver.country,
          weight: summarizePackages(values.packages, values.service).chargeableWeight || toNumber(values.shipment.grossWeight),
          type: 'PACK',
          length: toNumber(biggest?.length),
          width: toNumber(biggest?.width),
          height: toNumber(biggest?.height)
        });
        matched = rates.find(r => r.name === values.service.carrier || values.service.carrier.startsWith(r.name)) ?? null;
      } catch {
        // Không tra được giá thì vẫn cho tạo đơn — phụ phí sẽ được CS xác nhận sau
      }
    }

    const notable = warnings.filter(w => w.level === 'warning');
    if (matched?.hasSurcharge || notable.length) setConfirm({ quote: matched, warnings: notable });
    else proceed(values);
  };

  const onInvalid = (errors: FieldErrors<CreateOrderValues>) => {
    if (isWizard) {
      const firstBad = STEP_FIELDS.findIndex(fields => fields.some(f => errors[f]));
      if (firstBad >= 0) goToErrorStep(firstBad);
    }
    // Nói rõ ô nào sai — ô có thể nằm ở phần khách không đang nhìn (vd dữ liệu cũ của đơn nháp: email "=", SĐT lẫn chữ).
    const list = listFormErrors(errors);
    const shown = list.slice(0, 3).map(e => `${t(e.label)}: ${t(e.message)}`).join(' · ');
    toast.show(list.length ? `${t('Còn ô chưa đúng')} — ${shown}${list.length > 3 ? ' …' : ''}` : 'Còn ô bắt buộc chưa điền — đã tô đỏ giúp bạn', 'error');
    focusFirstError();
  };

  // Tự đăng xuất vì không thao tác: đơn đang làm có thay đổi chưa lưu → lưu vào nháp trước, đăng nhập lại mở trang Đơn nháp.
  const idleSave = useRef<() => Promise<string | void>>(async () => undefined);
  idleSave.current = async () => {
    if (persisting.current || !form.formState.isDirty) return;
    const draft = buildDraftPayload(form.getValues(), 'draft');
    if (draftId) await updateDraft.mutateAsync({ id: draftId, draft });
    else await saveDraft.mutateAsync(draft);
    finishAutosave(undefined);
    return '/drafts';
  };
  useEffect(() => onBeforeIdleLogout(() => idleSave.current()), []);

  return {
    submit: form.handleSubmit(onValid, onInvalid),
    saveAsDraft: () => persist(form.getValues(), 'draft'),
    saving,
    checkingQuote: quote.isPending,
    printing: issueAndPrint.isPending,
    busy: saving || quote.isPending,
    /** Hộp xác nhận phụ phí / cảnh báo kiện. */
    confirm,
    cancelConfirm: () => setConfirm(null),
    acceptConfirm: () => proceed(form.getValues()),
    /** Hộp Điều khoản dịch vụ: đồng ý → lưu + cấp bill + in; không đồng ý (Hủy / đóng) → vẫn lưu, đơn nằm ở Đơn nháp & chưa in. */
    termsOpen: !!termsFor,
    acceptTerms: () => {
      if (termsFor) createAndPrint(termsFor);
      setTermsFor(null);
    },
    declineTerms: () => {
      if (termsFor) persist(termsFor, 'ready');
      setTermsFor(null);
    }
  };
}
