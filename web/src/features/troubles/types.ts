/** Sự cố — hợp đồng API: docs/API_CONTRACT.md §6. */
export type TroublePriority = 'low' | 'mid' | 'high';
export type TroubleStatus = 'new' | 'doing' | 'waitc' | 'done';

/** 1 lượt trao đổi khách ↔ Việt An. `status`: trạng thái mới nếu lượt này đổi trạng thái. */
export interface TroubleMessage {
  from: 'customer' | 'vietan';
  author: string;
  message: string;
  date: string;
  status?: TroubleStatus | null;
}

export interface TroubleTicket {
  /** VD TRB-1024 */
  id: string;
  bill: string;
  cnee: string;
  ct: string;
  type: string;
  lv: TroublePriority;
  desc: string;
  /** Người yêu cầu */
  req: string;
  contact: string;
  date: string;
  status: TroubleStatus;
  /** Phản hồi gần nhất của CS */
  reply?: string | null;
  /** Lịch sử trao đổi, cũ trước. */
  messages?: TroubleMessage[];
}

export interface NewTrouble {
  bill: string;
  cnee?: string;
  ct?: string;
  type: string;
  lv: TroublePriority;
  desc: string;
  req: string;
  contact: string;
}
