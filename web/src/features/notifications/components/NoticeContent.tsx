import { useEffect, useId, useState } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, ExternalLinkButton, Icon, Modal } from '@/shared/ui';
import { notificationImageUrl } from '../api';
import type { InboxPart } from '../lib/inbox';
import { safeLink } from '../lib/link';
import { isRichText, sanitizeHtml } from '../lib/richText';
import styles from './NoticeContent.module.css';

interface NoticeContentProps {
  title: string;
  /** Dòng nhỏ dưới tiêu đề, vd "Việt An Express · 24/10/2026 08:30". */
  meta: string;
  imp?: boolean;
  pin?: boolean;
  parts: InboxPart[];
}

/**
 * Nội dung 1 thông báo như khách đọc: nhãn, tiêu đề, mục lục (khi có nhiều tin), từng tin với ảnh cỡ thật và nút link riêng.
 * Dùng chung cho khung chi tiết, popup tin quan trọng và xem trước ở trang quản trị. Bấm ảnh → xem lớn.
 */
export function NoticeContent({ title, meta, imp, pin, parts }: NoticeContentProps) {
  const { t } = useI18n();
  const prefix = useId();
  const [zoom, setZoom] = useState<string | null>(null);
  const [detail, setDetail] = useState<InboxPart | null>(null);
  const many = parts.length > 1;

  useEffect(() => {
    if (!zoom) return;
    const onKey = (e: KeyboardEvent) => e.key === 'Escape' && (e.stopPropagation(), setZoom(null));
    window.addEventListener('keydown', onKey, true);
    return () => window.removeEventListener('keydown', onKey, true);
  }, [zoom]);

  return (
    <article className={styles.notice}>
      {(pin || imp) && (
        <div className={styles.chips}>
          {pin && <span className={cx(styles.chip, styles.chipPin)}>{t('Ghim')}</span>}
          {imp && <span className={cx(styles.chip, styles.chipImp)}>{t('Quan trọng')}</span>}
        </div>
      )}
      <h2 className={styles.title}>{title}</h2>
      <p className={styles.meta}>{meta}</p>

      {many && (
        <nav className={styles.toc} aria-label={t('Các tin trong thông báo')}>
          <strong>{t('Gồm {n} tin', { n: parts.length })}</strong>
          {parts.map((p, i) => (
            <a key={i} href={`#${prefix}-${i}`} onClick={e => { e.preventDefault(); document.getElementById(`${prefix}-${i}`)?.scrollIntoView({ behavior: 'smooth', block: 'start' }); }}>
              <span>{i + 1}.</span> {p.title}
            </a>
          ))}
        </nav>
      )}

      {parts.map((p, i) => {
        const link = safeLink(p.link);
        return (
          <section key={i} id={`${prefix}-${i}`} className={cx(styles.part, many && styles.partMany)}>
            {many && <div className={styles.partNo}>{t('Tin {i} / {n}', { i: i + 1, n: parts.length })}</div>}
            {p.title && <h3 className={styles.partTitle}>{p.title}</h3>}
            <RichBody value={p.body} />
            {p.images.length > 0 && (
              <div className={cx(styles.gallery, p.images.length === 2 && styles.galleryTwo)}>
                {p.images.map(name => (
                  <button key={name} type="button" className={styles.image} onClick={() => setZoom(notificationImageUrl(name))} aria-label={t('Xem ảnh lớn')}>
                    <img src={notificationImageUrl(name)} alt="" loading="lazy" />
                  </button>
                ))}
              </div>
            )}
            {(p.detail || link) && (
              <div className={styles.link}>
                {p.detail ? (
                  <Button variant="primary" onClick={() => setDetail(p)}>
                    {t('Xem chi tiết')} <Icon name="arrowRight" size={15} />
                  </Button>
                ) : (
                  <ExternalLinkButton variant="primary" href={link!}>
                    {t('Xem chi tiết')} <Icon name="arrowRight" size={15} />
                  </ExternalLinkButton>
                )}
              </div>
            )}
          </section>
        );
      })}

      <Modal open={!!detail} title={detail?.title || title} size="lg" onClose={() => setDetail(null)}>
        {detail?.detail && <RichBody value={detail.detail} />}
      </Modal>

      {zoom && (
        <button type="button" className={styles.zoom} onClick={() => setZoom(null)} aria-label={t('Đóng ảnh')}>
          <img src={zoom} alt="" />
        </button>
      )}
    </article>
  );
}

/** Nội dung tin: HTML có định dạng (lọc lại trước khi hiện) hoặc chữ thường giữ xuống dòng. */
export function RichBody({ value }: { value: string }) {
  return isRichText(value)
    ? <div className={cx(styles.body, styles.rich)} dangerouslySetInnerHTML={{ __html: sanitizeHtml(value) }} />
    : <div className={styles.body}>{value}</div>;
}
