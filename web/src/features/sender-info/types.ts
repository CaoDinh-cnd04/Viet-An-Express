export interface SenderInfo { companyName: string; contactName: string | null; phone: string | null; email: string | null; address: string | null; taxCode: string | null }
export type SenderStatus = 'pending' | 'approved' | 'rejected' | 'cancelled';
export interface SenderRequest { id: number; status: SenderStatus; old: SenderInfo; new: SenderInfo; changed: (keyof SenderInfo)[]; note: string | null; reason: string | null; createdAt: string; reviewedAt: string | null }
export interface SenderOverview { current: SenderInfo; pending: SenderRequest | null; history: SenderRequest[] }
export interface AdminSenderRequest { request: SenderRequest; customerCode: string; companyName: string; userName: string; live: SenderInfo | null; outdated: boolean; reviewedBy: string | null }
export const SENDER_FIELDS = [
  { key: 'companyName', label: 'Tên công ty', max: 250, required: true },
  { key: 'contactName', label: 'Người liên hệ', max: 150, required: true },
  { key: 'phone', label: 'Điện thoại', max: 50, required: true },
  { key: 'email', label: 'Email', max: 150, required: false },
  { key: 'address', label: 'Địa chỉ lấy hàng', max: 250, required: true },
  { key: 'taxCode', label: 'Mã số thuế', max: 20, required: false }
] as const;
export const SENDER_STATUS = {
  pending: { label: 'Chờ duyệt', tone: 'warning' }, approved: { label: 'Đã duyệt', tone: 'success' },
  rejected: { label: 'Từ chối', tone: 'danger' }, cancelled: { label: 'Đã hủy', tone: 'neutral' }
} as const;
