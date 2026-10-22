import { useDeferredValue, useEffect, useMemo, useRef, useState } from 'react';
import { FormProvider, useWatch, type FieldErrors } from 'react-hook-form';
import { useLocation, useNavigate } from 'react-router-dom';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { orderUsage } from '@/shared/lib/usage';
import { Button, Card, LinkButton, Notice, PageHeader, SegmentedControl, useToast } from '@/shared/ui';
import { DraftsTable, useSaveDraft, useUpdateDraft } from '@/features/drafts';
import { useSession } from '@/features/auth';
import { useQuote, type ServiceQuote } from '@/features/pricing';
import { AddonsSection } from '../components/AddonsSection';
import { HelpLinksMenu } from '../components/HelpLinksMenu';
import { InvoiceSection } from '../components/InvoiceSection';
import { PackagesTable } from '../components/PackagesTable';
import { ReceiverSection } from '../components/ReceiverSection';
import { ServiceSection } from '../components/ServiceSection';
import { ShipmentSection } from '../components/ShipmentSection';
import { ShipperSection } from '../components/ShipperSection';
import { QuickLookup } from '../components/QuickLookup';
import { StepIndicator, type StepSection } from '../components/StepIndicator';
import { SurchargeConfirmDialog } from '../components/SurchargeConfirmDialog';
import { WIZARD_STEPS, WIZARD_STEPS_DOC } from '../constants';
import { useCreateOrderForm } from '../hooks/useCreateOrderForm';
import { autosaveKey, readAutosave, useOrderPrefill } from '../hooks/useOrderPrefill';
import { useShipmentSync } from '../hooks/useShipmentSync';
import { evaluatePackages, hasCritical, type PackageWarning } from '../lib/carrier-limits';
import { buildDraftPayload, summarizePackages, toNumber } from '../lib/shipment';
import { stepProgress } from '../lib/step-progress';
import { listFormErrors } from '../lib/form-errors';
import { STEP_FIELDS, type CreateOrderValues } from '../schema';
import styles from './CreateOrderPage.module.css';

export type CreateMode = 'wizard' | 'quick';

const MODE_OPTIONS = [
  { value: 'wizard', label: 'Từng bước' },
  { value: 'quick', label: '1 trang' }
] as const;

const MODE_ROUTES: Record<CreateMode, string> = { wizard: '/orders/new', quick: '/orders/new/quick' };
const PACKAGES_STEP = 1;

/** Id phần tử của từng khung trong form — mục lục bấm để cuộn tới, theo dõi khung đang xem khi cuộn. */
const secId = (key: string) => `order-sec-${key}`;

/** Khoảng cách từ đỉnh màn hình (dưới thanh trên cùng) để tính khung "đang xem". */
const SPY_LINE = 140;

/** Các khung con của từng bước, theo đúng thứ tự trên trang. */
function stepSections(isPack: boolean): StepSection[][] {
  return [
    [
      { id: secId('shipper'), label: 'Người gửi' },
      { id: secId('receiver'), label: 'Người nhận' },
      { id: secId('service'), label: 'Dịch vụ' },
      { id: secId('shipment'), label: 'Thông tin đơn hàng' }
    ],
    [
      // Hàng hóa: nhóm hàng khai ngay trong "Chi tiết kiện hàng"; chứng từ: khung "Nội dung chứng từ"
      ...(isPack ? [{ id: secId('packages'), label: 'Chi tiết kiện hàng' }] : []),
      { id: secId('addons'), label: 'Tùy chọn dịch vụ' }
    ],
    ...(isPack ? [[{ id: secId('invoice'), label: 'Invoice (khai báo hải quan)' }]] : [])
  ];
}

/** Khung đang xem: khung cuối cùng đã cuộn qua vạch SPY_LINE; cuộn hết trang thì là khung cuối có trên trang. */
function useActiveSection(ids: readonly string[]) {
  const [active, setActive] = useState<string>();
  const key = ids.join('|');
  useEffect(() => {
    const list = key.split('|');
    const update = () => {
      const present = list.filter(id => document.getElementById(id));
      let current = present[0];
      for (const id of present) if (document.getElementById(id)!.getBoundingClientRect().top <= SPY_LINE) current = id;
      const atBottom = window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 4;
      setActive(atBottom ? present[present.length - 1] : current);
    };
    update();
    window.addEventListener('scroll', update, { passive: true });
    window.addEventListener('resize', update);
    return () => {
      window.removeEventListener('scroll', update);
      window.removeEventListener('resize', update);
    };
  }, [key]);
  return active;
}

/** Cuộn tới ô lỗi đầu tiên sau khi kiểm tra không đạt — đợi khung đang thu gọn (vd người gửi) tự mở ra trước. */
const focusFirstError = () =>
  window.setTimeout(() => {
    const el = [...document.querySelectorAll<HTMLElement>('[aria-invalid="true"]')].find(e => e.offsetParent !== null);
    el?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    el?.focus({ preventScroll: true });
  }, 120);

/**
 * Trang tạo đơn. Hai chế độ dùng chung một form và các khối con:
 * - wizard: 3 bước, kiểm tra từng bước trước khi sang bước sau.
 * - quick : mọi khối trên 1 trang, kèm bảng đơn nháp bên dưới.
 * Tạo đơn = lưu vào "Đơn nháp & chưa in" (status 'ready'); bấm In ở đó mới cấp mã bill.
 * Dữ liệu tự lưu trong phiên nên chuyển chế độ / tải lại trang không mất.
 */
export default function CreateOrderPage({ mode }: { mode: CreateMode }) {
  const navigate = useNavigate();
  const { search, pathname } = useLocation();
  const toast = useToast();
  const { t } = useI18n();
  const session = useSession();
  const storageKey = autosaveKey(session.data?.status === 'authenticated' ? session.data.user : null);
  const editingExisting = useRef(new URLSearchParams(search).has('draft'));
  const recordUsage = (kind: 'order_start' | 'order_complete') => {
    if (!editingExisting.current && session.data?.status === 'authenticated') orderUsage(storageKey, mode, kind);
  };
  const [initial] = useState(() => (search ? undefined : readAutosave(storageKey)));
  const form = useCreateOrderForm(initial);
  const { draftId, finishAutosave, retainDraft, copyLoading, copyError, retryCopy } = useOrderPrefill(form, storageKey);
  const persisting = useRef(false);
  const { onShipmentInput, onPackagesInput, onTypeChange, docConverted } = useShipmentSync(form);
  const saveDraft = useSaveDraft();
  const updateDraft = useUpdateDraft();
  const quote = useQuote();
  const [step, setStep] = useState(0);
  const [confirm, setConfirm] = useState<{ quote: ServiceQuote | null; warnings: PackageWarning[] } | null>(null);

  const isWizard = mode === 'wizard';
  // Chứng từ (DOC): chỉ khai nội dung — ẩn bảng kiện và Invoice, wizard còn 2 bước. Hàng hoá (PACK): đủ 3 bước.
  const isPack = useWatch({ control: form.control, name: 'shipment.type' }) === 'PACK';
  const steps = isPack ? WIZARD_STEPS : WIZARD_STEPS_DOC;
  const lastStep = steps.length - 1;
  const current = Math.min(step, lastStep); // đang ở bước Invoice mà đổi sang DOC → lùi về bước cuối
  const showStep = (i: number) => !isWizard || current === i;
  const saving = saveDraft.isPending || updateDraft.isPending;
  const sections = stepSections(isPack);
  const activeSection = useActiveSection(sections.flat().map(sec => sec.id));
  // 1 trang: bước đang xem = bước chứa khung đang xem
  const viewingStep = Math.max(0, sections.findIndex(list => list.some(sec => sec.id === activeSection)));
  // Bước nào đã điền đủ (kiểm tra theo schema, trễ 1 nhịp để gõ không bị khựng)
  const watched = useDeferredValue(useWatch({ control: form.control }) as CreateOrderValues);
  const completed = useMemo(() => stepProgress(watched, steps.length), [watched, steps.length]);
  const scrollToId = (id: string) => document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  const selectSection = async (i: number, id: string) => {
    if (isWizard && i !== current) {
      await goToStep(i);
      // đợi bước mới hiện ra rồi mới cuộn tới khung
      window.setTimeout(() => scrollToId(id), 80);
    } else scrollToId(id);
  };

  const goToStep = async (target: number) => {
    // Lùi bước luôn được; tiến bước phải qua kiểm tra các bước đang đứng
    if (target > current) {
      for (let s = current; s < target; s++) {
        const ok = await form.trigger([...STEP_FIELDS[s]!]);
        if (!ok) {
          setStep(s);
          focusFirstError();
          return;
        }
      }
    }
    setStep(target);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  };

  const persist = (values: CreateOrderValues, status: 'draft' | 'ready') => {
    if (persisting.current) return;
    persisting.current = true;
    const payload = buildDraftPayload(values, status);
    const onSuccess = (saved: { id: string }) => {
      if (status === 'ready') recordUsage('order_complete');
      finishAutosave(status === 'ready' ? undefined : { keepEditing: values });
      setConfirm(null);
      toast.show(status === 'ready' ? 'Đã tạo đơn. Bấm "In & cấp bill" để cấp mã bill và in vận đơn A4.' : 'Đã lưu nháp.', 'success');
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

  /** Trước khi tạo: chặn kiện hãng không nhận; hỏi xác nhận nếu có phụ phí / cảnh báo. */
  const onValid = async (values: CreateOrderValues) => {
    const isPack = values.shipment.type === 'PACK';
    const warnings = isPack ? evaluatePackages(values.service.carrier, values.packages) : [];
    if (hasCritical(warnings)) {
      if (isWizard) setStep(PACKAGES_STEP);
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
    else persist(values, 'ready');
  };

  const onInvalid = (errors: FieldErrors<CreateOrderValues>) => {
    if (isWizard) {
      const firstBad = STEP_FIELDS.findIndex(fields => fields.some(f => errors[f]));
      if (firstBad >= 0) setStep(firstBad);
    }
    // Nói rõ ô nào sai — ô có thể nằm ở phần khách không đang nhìn (vd dữ liệu cũ của đơn nháp: email "=", SĐT lẫn chữ).
    const list = listFormErrors(errors);
    const shown = list.slice(0, 3).map(e => `${t(e.label)}: ${t(e.message)}`).join(' · ');
    toast.show(list.length ? `${t('Còn ô chưa đúng')} — ${shown}${list.length > 3 ? ' …' : ''}` : 'Còn ô bắt buộc chưa điền — đã tô đỏ giúp bạn', 'error');
    focusFirstError();
  };

  const submit = form.handleSubmit(onValid, onInvalid);
  const saveAsDraft = () => persist(form.getValues(), 'draft');
  const busy = saving || quote.isPending;

  if (copyLoading || copyError) return <>
    <PageHeader title="Copy đơn" description="Điền sẵn toàn bộ thông tin đơn gốc để tạo đơn mới." />
    {copyError ? <Notice tone="danger">{getErrorMessage(copyError, 'Không tải được đơn gốc để copy')} <Button onClick={retryCopy}>{t('Thử lại')}</Button></Notice>
      : <p role="status">{t('Đang tải toàn bộ thông tin đơn gốc…')}</p>}
  </>;

  return (
    <FormProvider {...form}>
      <PageHeader
        title={draftId ? 'Sửa đơn nháp' : 'Tạo đơn hàng'}
        description={isWizard ? t('{n} bước · tự lưu trong phiên', { n: steps.length }) : 'Điền tất cả trên 1 trang · tự lưu trong phiên'}
        actions={
          <>
            <HelpLinksMenu />
            <SegmentedControl ariaLabel="Chế độ tạo đơn" options={MODE_OPTIONS} value={mode} onChange={m => navigate(MODE_ROUTES[m] + search)} />
          </>
        }
      />

      <div className={styles.layout}>
      {/* Mục lục dọc, ghim theo khi cuộn: từng bước → chuyển bước; 1 trang → cuộn tới phần đó và đánh dấu phần đang xem */}
      <aside className={styles.nav}>
        <StepIndicator
          steps={steps}
          sections={sections}
          current={isWizard ? current : viewingStep}
          activeSection={activeSection}
          showDone={isWizard}
          completed={completed}
          onSelectStep={i => (isWizard ? void goToStep(i) : scrollToId(sections[i]![0]!.id))}
          onSelectSection={(i, id) => void selectSection(i, id)}
        />
        <QuickLookup />
      </aside>

      <form className={styles.form} onChangeCapture={() => recordUsage('order_start')} onSubmit={e => void submit(e)} noValidate>
        {/* Giao diện dọc: mỗi khung 1 hàng theo thứ tự khai bill, có id để mục lục nhảy tới */}
        {showStep(0) && (
          <>
            <div id={secId('shipper')} className={styles.section}><ShipperSection /></div>
            <div id={secId('receiver')} className={styles.section}><ReceiverSection /></div>
            <div id={secId('service')} className={styles.section}><ServiceSection /></div>
            <div id={secId('shipment')} className={styles.section}>
              <ShipmentSection onShipmentInput={onShipmentInput} onTypeChange={onTypeChange} docConverted={docConverted} />
            </div>
          </>
        )}

        {showStep(1) && (
          <>
            {isPack && <div id={secId('packages')} className={styles.section}><PackagesTable onPackagesInput={onPackagesInput} /></div>}
            <div id={secId('addons')} className={styles.section}><AddonsSection /></div>
          </>
        )}

        {isPack && showStep(2) && <div id={secId('invoice')} className={styles.section}><InvoiceSection /></div>}

        <footer className={styles.footer}>
          {isWizard && current > 0 && <Button onClick={() => void goToStep(current - 1)}>{t('Quay lại')}</Button>}
          <Button variant="ghost" onClick={saveAsDraft} disabled={busy}>{t('Lưu nháp')}</Button>
          <span className={styles.spacer} />
          {isWizard && current < lastStep ? (
            <Button variant="primary" onClick={() => void goToStep(current + 1)}>
              {t('Tiếp tục: {step}', { step: t(steps[current + 1]!.title) })}
            </Button>
          ) : (
            <Button variant="primary" type="submit" disabled={busy}>
              {t(quote.isPending ? 'Đang kiểm tra phụ phí…' : saving ? 'Đang lưu…' : draftId ? 'Lưu & sẵn sàng in' : 'Tạo đơn hàng')}
            </Button>
          )}
        </footer>
      </form>
      </div>

      {!isWizard && (
        <Card flush title="Đơn nháp & chưa in" subtitle="· bấm In để cấp mã bill" actions={<LinkButton to="/drafts" size="sm" variant="ghost">{t('Mở trang đầy đủ')}</LinkButton>} className={styles.drafts}>
          <DraftsTable limit={10} />
        </Card>
      )}

      <SurchargeConfirmDialog
        open={!!confirm}
        quote={confirm?.quote ?? null}
        warnings={confirm?.warnings ?? []}
        busy={saving}
        onCancel={() => setConfirm(null)}
        onConfirm={() => persist(form.getValues(), 'ready')}
      />
    </FormProvider>
  );
}
