/** Ảnh đính kèm góp ý — tải qua GET /account/feedback/images/{id}. */
export interface FeedbackImage {
  id: string;
  fileName: string;
  size: number;
}

/** 1 câu trả lời khảo sát — lưu dbo.GopY.Khao_Sat (kèm nội dung câu hỏi lúc gửi). */
export interface SurveyAnswer {
  id: string;
  question: string;
  answers: string[];
  note?: string | null;
}

/** GET /account/feedback — góp ý khách đã gửi. */
export interface FeedbackItem {
  id: string;
  message: string;
  contact?: string | null;
  /** 1–5 sao, null = không chấm. */
  rating?: number | null;
  /** 'dd/MM/yyyy HH:mm' */
  createdAt: string;
  seen: boolean;
  images: FeedbackImage[];
  /** Câu trả lời khảo sát (rỗng nếu khách không trả lời). */
  survey?: SurveyAnswer[];
}

/** GET /admin/feedback — góp ý của mọi khách (trang quản trị). */
export interface AdminFeedbackItem extends FeedbackItem {
  customerCode: string;
  companyName: string;
  userName: string;
  isStaff: boolean;
}

/** GET /admin/feedback/{id} — chi tiết 1 góp ý kèm liên hệ của khách. */
export interface AdminFeedbackDetail {
  feedback: AdminFeedbackItem;
  seenAt?: string | null;
  customerContact?: string | null;
  customerPhone?: string | null;
  customerEmail?: string | null;
  customerAddress?: string | null;
}
