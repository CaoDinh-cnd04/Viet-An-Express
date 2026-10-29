import { useDeferredValue, useMemo, useState } from 'react';
import { useWatch, type UseFormReturn } from 'react-hook-form';
import { STEP_FIELDS, type CreateOrderValues } from '@/features/order-form';
import { WIZARD_STEPS, WIZARD_STEPS_DOC } from '../constants';
import { stepSections } from '../lib/order-sections';
import { focusFirstError, scrollToId } from '../lib/scroll';
import { stepProgress } from '../lib/step-progress';
import { useActiveSection } from './useActiveSection';

/**
 * Điều hướng bước / mục lục của trang tạo đơn.
 * - wizard: hiện từng bước, sang bước sau phải qua kiểm tra các bước đang đứng; lùi bước luôn được.
 * - 1 trang: mọi khung hiện cùng lúc, bước "đang xem" = bước chứa khung đang cuộn tới.
 */
export function useWizardSteps(form: UseFormReturn<CreateOrderValues>, isWizard: boolean) {
  const [step, setStep] = useState(0);
  // Chứng từ (DOC): chỉ khai nội dung — ẩn bảng kiện và Invoice, wizard còn 2 bước. Hàng hoá (PACK): đủ 3 bước.
  const isPack = useWatch({ control: form.control, name: 'shipment.type' }) === 'PACK';
  const steps = isPack ? WIZARD_STEPS : WIZARD_STEPS_DOC;
  const lastStep = steps.length - 1;
  const current = Math.min(step, lastStep); // đang ở bước Invoice mà đổi sang DOC → lùi về bước cuối
  const sections = stepSections(isPack);
  const activeSection = useActiveSection(sections.flat().map(sec => sec.id));
  // 1 trang: bước đang xem = bước chứa khung đang xem
  const viewingStep = Math.max(0, sections.findIndex(list => list.some(sec => sec.id === activeSection)));
  // Bước nào đã điền đủ (kiểm tra theo schema, trễ 1 nhịp để gõ không bị khựng)
  const watched = useDeferredValue(useWatch({ control: form.control }) as CreateOrderValues);
  const completed = useMemo(() => stepProgress(watched, steps.length), [watched, steps.length]);

  const goToStep = async (target: number) => {
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

  const selectSection = async (i: number, id: string) => {
    if (isWizard && i !== current) {
      await goToStep(i);
      // đợi bước mới hiện ra rồi mới cuộn tới khung
      window.setTimeout(() => scrollToId(id), 80);
    } else scrollToId(id);
  };

  const selectStep = (i: number) => (isWizard ? void goToStep(i) : scrollToId(sections[i]![0]!.id));

  return {
    isPack,
    steps,
    current,
    lastStep,
    sections,
    activeSection,
    /** Bước tô sáng trên mục lục. */
    highlighted: isWizard ? current : viewingStep,
    completed,
    showStep: (i: number) => !isWizard || current === i,
    goToStep,
    selectStep,
    selectSection,
    /** Nhảy thẳng tới bước (không kiểm tra) — khi lỗi nằm ở bước đó. */
    setStep
  };
}
