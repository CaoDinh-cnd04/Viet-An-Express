import { useState } from 'react';
import { FormProvider } from 'react-hook-form';
import { useLocation, useNavigate } from 'react-router-dom';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
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
import { SectionTitle } from '../components/SectionTitle';
import { ShipperSection } from '../components/ShipperSection';
import formStyles from '../components/form.module.css';
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
            {/* data-tour: mốc cho hướng dẫn từng nút của trang (features/onboarding) */}
            <span data-tour="create-help-links"><HelpLinksMenu /></span>
            <span data-tour="create-mode"><SegmentedControl ariaLabel="Chế độ tạo đơn" options={MODE_OPTIONS} value={mode} onChange={m => navigate(MODE_ROUTES[m] + search)} /></span>
          </>
        }
      />

      <div className={styles.layout}>
        {/* Thanh các bước ngang trên cùng (trải 2 cột): từng bước → chuyển bước; 1 trang → cuộn tới phần đó */}
        <div className={styles.steps} data-tour="create-nav">
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
        </div>
        {/* Cột trái ghim theo khi cuộn: khung Hỗ trợ khách hàng */}
        <aside className={styles.nav}>
          <div data-tour="create-lookup"><QuickLookup /></div>
        </aside>

        <form className={styles.form} onSubmit={e => void order.submit(e)} noValidate>
          {/* Bước đầu chia 2 cột: trái người gửi → hàng & dịch vụ, phải người nhận; mỗi phần có id để thanh các bước nhảy tới */}
          {showStep(0) && (
            <div className={styles.parties}>
              <div className={styles.column}>
                <div id={secId('shipper')} className={styles.section} data-tour="create-shipper"><ShipperSection /></div>
                <div className={styles.section}>
                  <Card accent className={formStyles.cleanCard} title={<SectionTitle plain icon="truck" title="Hàng & dịch vụ" />}>
                    <div id={secId('service')} className={styles.section} data-tour="create-service"><ServiceSection /></div>
                    <div id={secId('shipment')} className={styles.section} data-tour="create-shipment">
                      <ShipmentSection onShipmentInput={onShipmentInput} onTypeChange={onTypeChange} docConverted={docConverted} />
                    </div>
                  </Card>
                </div>
              </div>
              <div id={secId('receiver')} className={cx(styles.section, styles.fillColumn)} data-tour="create-receiver"><ReceiverSection /></div>
            </div>
          )}

          {showStep(1) && (
            <>
              {/* Hàng hóa: dịch vụ thêm là 1 dòng cuối khung kiện hàng; chứng từ (không có bảng kiện): khung riêng */}
              {isPack ? (
                <div id={secId('packages')} className={styles.section} data-tour="create-packages">
                  <PackagesTable onPackagesInput={onPackagesInput}>
                    <div id={secId('addons')} className={styles.section} data-tour="create-addons"><AddonsSection embedded /></div>
                  </PackagesTable>
                </div>
              ) : (
                <div id={secId('addons')} className={styles.section} data-tour="create-addons"><AddonsSection /></div>
              )}
            </>
          )}

          {isPack && showStep(2) && <div id={secId('invoice')} className={styles.section} data-tour="create-invoice"><InvoiceSection /></div>}

          <footer className={styles.footer} data-tour="create-actions">
            {isWizard && current > 0 && <Button onClick={() => void wizard.goToStep(current - 1)}>{t('Quay lại')}</Button>}
            <Button className={styles.draftButton} onClick={order.saveAsDraft} disabled={order.busy}>{t('Lưu nháp')}</Button>
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
        <div data-tour="create-drafts">
          <Card flush title="Đơn nháp & chưa in" subtitle="· bấm In để cấp mã bill" actions={<LinkButton to="/drafts" size="sm" variant="ghost">{t('Mở trang đầy đủ')}</LinkButton>} className={styles.drafts}>
            <DraftsTable limit={10} />
          </Card>
        </div>
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
