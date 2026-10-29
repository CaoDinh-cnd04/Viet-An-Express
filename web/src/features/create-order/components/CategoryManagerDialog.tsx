import { useState } from 'react';
import { getErrorMessage } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { cx } from '@/shared/lib/cx';
import { Button, Icon, Modal, useToast } from '@/shared/ui';
import { useCategories, useDeleteCategory, useFavoriteCategory, useSaveCategory, type Category } from '../api';
import { RULES } from '@/features/order-form';
import styles from './form.module.css';

interface CategoryManagerDialogProps {
  open: boolean;
  onClose: () => void;
  /** Gọi sau khi thêm nhóm mới — vd chọn luôn nhóm vừa tạo. */
  onCreated?: (name: string) => void;
  /** Gọi sau khi đổi tên — vd cập nhật ô đang chọn nhóm cũ. */
  onRenamed?: (from: string, to: string) => void;
}

/**
 * Quản lý nhóm hàng hóa (dbo.NhomHangHoa): khách thêm / đổi tên / xóa nhóm của mình.
 * Đánh dấu yêu thích được cả nhóm chung của Việt An (lưu riêng cho khách) — nhóm yêu thích hiện ở đầu ô chọn nhóm.
 */
export function CategoryManagerDialog({ open, onClose, onCreated, onRenamed }: CategoryManagerDialogProps) {
  const { t } = useI18n();
  const toast = useToast();
  const { data: categories = [], isLoading, isError } = useCategories();
  const save = useSaveCategory();
  const remove = useDeleteCategory();
  const favorite = useFavoriteCategory();
  const [newName, setNewName] = useState('');
  const [editing, setEditing] = useState<{ id: string; name: string } | null>(null);
  const own = categories.filter(c => c.isOwn);
  const shared = categories.filter(c => !c.isOwn);
  const busy = save.isPending || remove.isPending || favorite.isPending;

  const fail = (e: unknown) => toast.show(getErrorMessage(e), 'error');

  // Hộp thoại nằm trong form tạo đơn: không dùng <form> lồng (submit sẽ lan lên form cha) — thêm bằng nút / phím Enter.
  const add = () => {
    const name = newName.trim();
    if (!name) return;
    save.mutate(
      { name, isFavorite: false },
      {
        onSuccess: res => {
          setNewName('');
          toast.show(res.message, 'success');
          onCreated?.(res.data.name);
        },
        onError: fail
      }
    );
  };

  const rename = (c: Category) => {
    const name = editing?.name.trim() ?? '';
    if (!name || name === c.name) return setEditing(null);
    save.mutate({ id: c.id, name, isFavorite: c.isFavorite }, { onSuccess: res => { setEditing(null); toast.show(res.message, 'success'); onRenamed?.(c.name, res.data.name); }, onError: fail });
  };

  const toggleFavorite = (c: Category) => favorite.mutate({ id: c.id, isFavorite: !c.isFavorite }, { onError: fail });

  const del = (c: Category) => {
    if (!window.confirm(t('Xóa nhóm "{name}"? Đơn đã khai nhóm này không bị ảnh hưởng.', { name: c.name }))) return;
    remove.mutate(c.id, { onSuccess: res => toast.show(res.message, 'success'), onError: fail });
  };

  return (
    <Modal open={open} size="lg" title="Quản lý nhóm hàng hóa" onClose={onClose} footer={<Button onClick={onClose}>{t('Đóng')}</Button>}>
      <div className={styles.inlineActions}>
        <input
          className={styles.pickerSearch}
          placeholder={t('Tên nhóm mới, vd: Áo dài, nón lá…')}
          aria-label={t('Tên nhóm mới')}
          maxLength={RULES.categoryNameMax}
          value={newName}
          onChange={e => setNewName(e.target.value)}
          onKeyDown={e => {
            if (e.key === 'Enter') { e.preventDefault(); add(); }
          }}
        />
        <Button variant="primary" size="sm" disabled={busy || !newName.trim()} onClick={add}>
          <Icon name="plus" size={15} /> {t('Thêm nhóm')}
        </Button>
      </div>

      {isError && <p className={styles.errorText} role="alert">{t('Không tải được danh sách nhóm hàng')}</p>}

      <h4 className={styles.spaced}>{t('Nhóm của bạn ({n})', { n: own.length })}</h4>
      {own.length === 0 ? (
        <p className={styles.hint}>{t(isLoading ? 'Đang tải…' : 'Chưa có nhóm riêng — thêm nhóm ở ô phía trên.')}</p>
      ) : (
        <div className={styles.tableScroll}>
          <table className={styles.table}>
            <tbody>
              {own.map(c => (
                <tr key={c.id}>
                  <td className={styles.colDel}>
                    <Button size="sm" iconOnly variant="ghost" disabled={busy} onClick={() => toggleFavorite(c)}
                      aria-pressed={c.isFavorite} aria-label={t(c.isFavorite ? 'Bỏ yêu thích' : 'Đánh dấu yêu thích')}
                      title={t(c.isFavorite ? 'Bỏ yêu thích' : 'Đánh dấu yêu thích — nhóm hiện lên đầu danh sách')}>
                      <Icon name="star" size={16} className={c.isFavorite ? styles.starOn : undefined} />
                    </Button>
                  </td>
                  <td>
                    {editing?.id === c.id ? (
                      <input
                        className={styles.cell}
                        autoFocus
                        aria-label={t('Tên nhóm {name}', { name: c.name })}
                        maxLength={RULES.categoryNameMax}
                        value={editing.name}
                        onChange={e => setEditing({ id: c.id, name: e.target.value })}
                        onKeyDown={e => {
                          if (e.key === 'Enter') { e.preventDefault(); rename(c); }
                          if (e.key === 'Escape') { e.stopPropagation(); setEditing(null); }
                        }}
                      />
                    ) : (
                      <span className={styles.cellText}>{c.name}</span>
                    )}
                  </td>
                  <td className={styles.colQty}>
                    {editing?.id === c.id ? (
                      <Button size="sm" variant="primary" disabled={busy} onClick={() => rename(c)}>{t('Lưu')}</Button>
                    ) : (
                      <Button size="sm" iconOnly variant="ghost" disabled={busy} aria-label={t('Sửa nhóm {name}', { name: c.name })} onClick={() => setEditing({ id: c.id, name: c.name })}>
                        <Icon name="edit" size={15} />
                      </Button>
                    )}
                    <Button size="sm" iconOnly variant="ghost" disabled={busy} aria-label={t('Xóa nhóm {name}', { name: c.name })} onClick={() => del(c)}>
                      <Icon name="trash" size={15} />
                    </Button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <h4 className={styles.spaced}>{t('Nhóm chung của Việt An ({n})', { n: shared.length })}</h4>
      <p className={styles.hint}>{t('Bấm ngôi sao để đưa nhóm vào mục "Nhóm yêu thích" ở đầu ô chọn nhóm hàng.')}</p>
      <div className={styles.catChips}>
        {shared.map(c => (
          <button
            key={c.id}
            type="button"
            className={cx(styles.catChip, c.isFavorite && styles.catChipOn)}
            disabled={busy}
            aria-pressed={c.isFavorite}
            title={t(c.isFavorite ? 'Bỏ yêu thích' : 'Đánh dấu yêu thích')}
            onClick={() => toggleFavorite(c)}
          >
            <Icon name="star" size={13} className={c.isFavorite ? styles.starOn : undefined} />
            {t(c.name)}
          </button>
        ))}
        {!shared.length && <span className={styles.hint}>—</span>}
      </div>
    </Modal>
  );
}
