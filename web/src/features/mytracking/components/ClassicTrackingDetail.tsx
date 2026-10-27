import { useState } from 'react';
import type { FoundTrackResult } from '@/features/landing';
import { Icon } from '@/shared/ui';
import { classicDate, classicPlace, classicStatus, groupClassic } from '../lib/classic';
import styles from './ClassicTrackingDetail.module.css';

const UPDATING = 'Updating';

/**
 * Kết quả 1 vận đơn trên trang MyTracking theo mẫu mytracking.top cũ (tiếng Anh như trang cũ):
 * khung Tracking number / Status / From / To / Ship date / Estimate date, bảng hành trình gom theo ngày, nút Hide detail.
 * Tiêu đề bảng theo màu thương hiệu (--color-tracking-bar), nền dòng thông tin / dòng ngày theo màu phụ (--color-tracking-row).
 */
export function ClassicTrackingDetail({ result: r }: { result: FoundTrackResult }) {
  const [open, setOpen] = useState(true);
  const days = groupClassic(r.events);
  const icon = r.status === 'ok' ? 'check' : r.status === 'nd' ? 'alert' : 'arrowRight';

  return (
    <article className={styles.detail} aria-labelledby={`classic-${r.bill}`}>
      <table className={styles.info}>
        <tbody>
          <tr>
            <th scope="row">Tracking number:</th>
            <td colSpan={3}><strong id={`classic-${r.bill}`} className={styles.bill}>{r.bill}</strong></td>
          </tr>
          <tr>
            <th scope="row">Status:</th>
            <td colSpan={3}>
              <span className={styles.status}>
                <span className={styles.statusIcon}><Icon name={icon} size={18} /></span>
                {classicStatus(r.events[0], UPDATING)}
              </span>
            </td>
          </tr>
          <tr>
            <th scope="row">From:</th>
            <td>{classicPlace(r.origin) || UPDATING}</td>
            <th scope="row">Ship date:</th>
            <td>{classicDate(r.shipDate) || UPDATING}</td>
          </tr>
          <tr>
            <th scope="row">To:</th>
            <td>{classicPlace(r.destination) || UPDATING}</td>
            <th scope="row">Estimate Date:</th>
            <td>{classicDate(r.estimatedDate) || UPDATING}</td>
          </tr>
        </tbody>
      </table>

      {open && (
        <table className={styles.events}>
          <thead>
            <tr><th scope="col">Date/time</th><th scope="col">Activity</th><th scope="col">Location</th></tr>
          </thead>
          <tbody>
            {days.length === 0 && <tr><td colSpan={3} className={styles.empty}>{UPDATING}</td></tr>}
            {days.map((d, di) => [
              <tr key={`d${di}`} className={styles.day}><td colSpan={3}>{d.date}</td></tr>,
              ...d.items.map((e, i) => (
                <tr key={`d${di}-${i}`}>
                  <td className={styles.time}>{e.time}</td>
                  <td>{e.title}</td>
                  <td>{e.location}</td>
                </tr>
              ))
            ])}
          </tbody>
        </table>
      )}
      <button type="button" className={styles.toggle} onClick={() => setOpen(v => !v)} aria-expanded={open}>
        {open ? 'Hide detail' : 'Show detail'} <span aria-hidden="true">{open ? '↑↑' : '↓↓'}</span>
      </button>
    </article>
  );
}
