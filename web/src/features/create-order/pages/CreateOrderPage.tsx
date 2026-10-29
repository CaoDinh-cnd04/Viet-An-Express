import { useState } from 'react';
import { FormProvider } from 'react-hook-form';
import { useLocation, useNavigate } from 'react-router-dom';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { Button, Card, LinkButton, Notice, PageHeader, SegmentedControl } from '@/shared/ui';
import { useSession } from '@/features/auth';
import { DraftsTable } from '@/features/drafts';
import { TermsDialog } from '@/features/terms';
import { AddonsSection } from '../components/AddonsSection';
import { HelpLinksMenu } from '../components/HelpLinksMenu';
import { InvoiceSection } from '../components/InvoiceSection';
import { PackagesTable } from '../components/PackagesTable';
import { QuickLookup } from '../components/QuickLookup';
import { ReceiverSection } from '../components/ReceiverSection';
import { ServiceSection } from '../components/ServiceSection';
import { ShipmentSection } from '../components/ShipmentSection';
import { ShipperSection } from '../components/ShipperSection';
import { StepIndicator } from '../components/StepIndicator';
import { SurchargeConfirmDialog } from '../components/SurchargeConfirmDialog';
import { useCreateOrderForm } from '../hooks/useCreateOrderForm';
import { autosaveKey, readAutosave, useOrderPrefill } from '../hooks/useOrderPrefill';
import { useOrderSubmit } from '../hooks/useOrderSubmit';
import { useShipmentSync } from '../hooks/useShipmentSync';
import { useWizardSteps } from '../hooks/useWizardSteps';
import { secId } from '../lib/order-sections';
import styles from './CreateOrderPage.module.css';

export type CreateMode = 'wizard' | 'quick';

const MODE_OPTIONS = [
  { value: 'wizard', label: 'Từng bước' },
  { value: 'quick', label: '1 trang' }
] as const;

const MODE_ROUTES: Record<CreateMode, string> = { wizard: '/orders/new', quick: '/orders/new/quick' };

/**
 * Trang tạo đơn. Hai chế độ dùng chung một form và các khối con:
 * - wizard: 3 bước, kiểm tra từng bước trước khi sang bước sau (useWizardSteps).
 * - quick : mọi khối trên 1 trang, kèm bảng đơn nháp bên dưới.
 * Tạo đơn = lưu vào "Đơn nháp & chưa in" (status 'ready'); bấm In ở đó mới cấp mã bill (useOrderSubmit).
 * Dữ liệu tự lưu trong phiên nên chuyển chế độ / tải lại trang không mất.
 */
export default function CreateOrderPage({ mode }: { mode: CreateMode }) {
  const navigate = useNavigate();
  const { search } = useLocation();
  const { t } = useI18n();
  const session = useSession();
  const storageKey = autosaveKey(session.data?.status === 'authenticated' ? session.data.user : null);
  const [initial] = useState(() => (search ? undefined : readAutosave(storageKey)));
  const form = useCreateOrderForm(initial);
  const { draftId, finishAutosave, retainDraft, copyLoading, copyError, retryCopy } = useOrderPrefill(form, storageKey);
  const { onShipmentInput, onPackagesInput, onTypeChange, docConverted } = useShipmentSync(form);
  const isWizard = mode === 'wizard';
  const wizard = useWizardSteps(form, isWizard);
  const order = useOrderSubmit({ form, isWizard, draftId, finishAutosave, retainDraft, goToErrorStep: wizard.setStep });
  const { isPack, steps, current, lastStep, showStep } = wizard;

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
            sections={wizard.sections}
            current={wizard.highlighted}
            activeSection={wizard.activeSection}
            showDone={isWizard}
            completed={wizard.completed}
            onSelectStep={wizard.selectStep}
            onSelectSection={(i, id) => void wizard.selectSection(i, id)}
          />
          <QuickLookup />
        </aside>

        <form className={styles.form} onSubmit={e => void order.submit(e)} noValidate>
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
            {isWizard && current > 0 && <Button onClick={() => void wizard.goToStep(current - 1)}>{t('Quay lại')}</Button>}
            <Button variant="ghost" onClick={order.saveAsDraft} disabled={order.busy}>{t('Lưu nháp')}</Button>
            <span className={styles.spacer} />
            {isWizard && current < lastStep ? (
              <Button variant="primary" onClick={() => void wizard.goToStep(current + 1)}>
                {t('Tiếp tục: {step}', { step: t(steps[current + 1]!.title) })}
              </Button>
            ) : (
              <Button variant="primary" type="submit" disabled={order.busy || order.printing}>
                {t(order.checkingQuote ? 'Đang kiểm tra phụ phí…' : order.saving ? 'Đang lưu…' : order.printing ? 'Đang in…' : 'Tạo đơn & in bill')}
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
        open={!!order.confirm}
        quote={order.confirm?.quote ?? null}
        warnings={order.confirm?.warnings ?? []}
        busy={order.saving}
        onCancel={order.cancelConfirm}
        onConfirm={order.acceptConfirm}
      />

      {/* Tạo đơn & in bill: đồng ý → lưu + cấp bill + in; không đồng ý (Hủy / đóng) → vẫn lưu, đơn nằm ở Đơn nháp & chưa in. */}
      <TermsDialog open={order.termsOpen} onClose={order.declineTerms} onAccept={order.acceptTerms} />
    </FormProvider>
  );
}
