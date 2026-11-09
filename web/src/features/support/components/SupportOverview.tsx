import type { CSSProperties } from 'react';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Icon, type IconName } from '@/shared/ui';
import styles from './SupportOverview.module.css';

export interface StatusTile<K extends string> {
  key: K;
  label: string;
  /** Biến màu trong tokens.css, vd var(--color-warning). */
  tone: string;
  icon: IconName;
}

interface SupportOverviewProps<K extends string> {
  tiles: ReadonlyArray<StatusTile<K>>;
  counts: Record<K, number>;
  value: K;
  onChange: (f: K) => void;
}

/** Ô trạng thái đầu trang: icon trong ô màu nhạt, nhãn, số; ô đang chọn nền nhạt + viền theo màu trạng thái. */
export function SupportOverview<K extends string>({ tiles, counts, value, onChange }: SupportOverviewProps<K>) {
  const { t } = useI18n();
  return (
    <div className={styles.tiles} style={{ '--tile-count': tiles.length } as CSSProperties} role="tablist" aria-label={t('Lọc yêu cầu')}>
      {tiles.map(tile => {
        const on = value === tile.key;
        return (
          <button
            key={tile.key}
            type="button"
            role="tab"
            aria-selected={on}
            className={cx(styles.tile, on && styles.on)}
            style={{ '--tone': tile.tone } as CSSProperties}
            onClick={() => onChange(tile.key)}
          >
            <span className={styles.icon}><Icon name={tile.icon} size={16} /></span>
            <span className={styles.text}>
              <span className={styles.label}>{t(tile.label)}</span>
              <span className={styles.count}>{counts[tile.key]}</span>
            </span>
            {on && <Icon name="arrowUpRight" size={13} className={styles.arrow} />}
          </button>
        );
      })}
    </div>
  );
}
