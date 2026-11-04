import { useSession } from '@/features/auth';
import { useI18n } from '@/shared/i18n';
import { formatNumber } from '@/shared/lib/format';
import { Button, DropdownMenu, Icon } from '@/shared/ui';
import { ECOM_SOURCES } from '../constants';
import { PRINT_KINDS, buildPrintHtml, type PrintKind } from '../lib/print-docs';
import { printHtml } from '../lib/print-window';
import type { EcomOrder, EcomSource } from '../types';

const pad = (n: number) => String(n).padStart(2, '0');
const nowText = () => {
  const d = new Date();
  return `${pad(d.getDate())}/${pad(d.getMonth() + 1)}/${d.getFullYear()} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

/** Nút "In" → nhãn dán kiện A6 / phiếu đóng gói A4 / bảng kê giao hàng A4 cho các đơn truyền vào. In ngay trên trình duyệt. */
export function PrintMenu({ orders, size = 'sm', variant }: { orders: ReadonlyArray<EcomOrder>; size?: 'sm'; variant?: 'primary' }) {
  const { t } = useI18n();
  const session = useSession().data;
  const user = session?.status === 'authenticated' ? session.user : undefined;

  const print = (kind: PrintKind) =>
    printHtml(
      buildPrintHtml(kind, orders, {
        sender: { companyName: user?.companyName ?? '', customerCode: user?.customerCode ?? '', phone: user?.phone, address: user?.address },
        printedAt: nowText(),
        t,
        formatNumber,
        sourceLabel: src => t(ECOM_SOURCES[src as EcomSource]?.label ?? src)
      })
    );

  return (
    <DropdownMenu
      items={PRINT_KINDS.map(k => ({ label: k.label, onSelect: () => print(k.kind) }))}
      trigger={({ open, toggle }) => (
        <Button size={size} variant={variant} onClick={toggle} disabled={!orders.length} aria-expanded={open} aria-haspopup="menu">
          {t('In')} <Icon name="chevronDown" size={14} />
        </Button>
      )}
    />
  );
}
