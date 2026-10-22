import { useCallback, useState } from 'react';
import type { UseFormReturn } from 'react-hook-form';
import { useI18n } from '@/shared/i18n';
import { useToast } from '@/shared/ui';
import { RULES } from '../constants';
import { applyDocWeightRule, summarizePackages, toNumber } from '../lib/shipment';
import type { CreateOrderValues } from '../schema';

const OPTS = { shouldDirty: true } as const;

/**
 * Đồng bộ cân nặng giữa bước 1 và bảng kiện (bước 2):
 * - Bảng có 1 dòng: sửa cân ở bước 1 → cập nhật cân/kiện của dòng đó (= tổng cân / SL dòng).
 * - Sửa bảng kiện → tổng cân ở bước 1 cập nhật theo; nhiều dòng thì ô cân bước 1 chỉ đọc.
 * Hàng hóa (PACK): "Số kiện" tự cộng theo SL các dòng kiện (PackagesTable), không sửa tay.
 * Chạy theo sự kiện nhập (không dùng effect) để tránh vòng lặp cập nhật qua lại.
 * Đồng thời áp quy tắc chứng từ / hàng hoá theo cân (applyDocWeightRule):
 * chứng từ > 2kg tự chuyển PACK; PACK do hệ thống tự chuyển mà cân về ≤ 2kg thì trả lại chứng từ.
 */
export function useShipmentSync(form: UseFormReturn<CreateOrderValues>) {
  const { getValues, setValue } = form;
  const toast = useToast();
  const { t } = useI18n();
  /** PACK hiện tại do hệ thống tự chuyển (không phải khách chọn) — quyết định có hiện thông báo / trả lại DOC không. */
  const [autoConverted, setAutoConverted] = useState(false);

  const applyTypeRule = useCallback(
    (wasAutoConverted: boolean) => {
      const { shipment } = getValues();
      const next = applyDocWeightRule({ type: shipment.type, autoConverted: wasAutoConverted }, shipment.grossWeight);
      setAutoConverted(next.autoConverted);
      if (next.change === 'toPack') {
        setValue('shipment.type', 'PACK', OPTS);
        // Chứng từ quá cân → hàng hóa: chuyển số kiện / cân sang dòng kiện đầu tiên
        const pieces = Math.max(1, Math.trunc(toNumber(shipment.pieces)));
        const gross = toNumber(shipment.grossWeight);
        if (getValues('packages').length === 1) {
          setValue('packages.0.qty', String(pieces), OPTS);
          setValue('packages.0.weight', gross ? String(Math.round((gross / pieces) * 100) / 100) : '', OPTS);
        }
        toast.show(t('Tài liệu trên {kg}kg được tính là hàng hóa — đã chuyển sang PACK.', { kg: RULES.docMaxWeightKg }));
      } else if (next.change === 'toDoc') {
        setValue('shipment.type', 'DOC', OPTS);
        toast.show(t('Cân nặng không quá {kg}kg — đã chuyển lại chứng từ (DOC).', { kg: RULES.docMaxWeightKg }));
      }
    },
    [getValues, setValue, toast, t]
  );

  /** Khách tự bấm chọn Chứng từ / Hàng hoá: là lựa chọn chủ động, không còn là "tự chuyển". */
  const onTypeChange = useCallback(() => applyTypeRule(false), [applyTypeRule]);

  /** Gọi khi người dùng sửa Số kiện / Cân nặng ở bước 1. */
  const onShipmentInput = useCallback(() => {
    applyTypeRule(autoConverted);

    const { shipment, packages } = getValues();
    if (packages.length !== 1) return;
    const rowQty = Math.max(1, Math.trunc(toNumber(packages[0]!.qty)));
    const gross = toNumber(shipment.grossWeight);
    setValue('packages.0.weight', gross ? String(Math.round((gross / rowQty) * 100) / 100) : '', OPTS);
  }, [applyTypeRule, autoConverted, getValues, setValue]);

  /** Gọi sau khi người dùng sửa / thêm / xóa dòng kiện. */
  const onPackagesInput = useCallback(() => {
    const summary = summarizePackages(getValues('packages'));
    setValue('shipment.grossWeight', summary.grossWeight ? String(summary.grossWeight) : '', OPTS);
  }, [getValues, setValue]);

  return { onShipmentInput, onPackagesInput, onTypeChange, docConverted: autoConverted };
}
