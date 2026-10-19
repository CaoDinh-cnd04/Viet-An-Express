import { zodResolver } from "@hookform/resolvers/zod";
import type { ReactNode } from "react";
import { Controller, useForm } from "react-hook-form";
import { useI18n } from "@/shared/i18n";
import { Button, Modal, Notice } from "@/shared/ui";
import {
  useAssignablePermissions,
  useCreateStaff,
  useUpdateStaff,
} from "../api";
import { groupPermissions, togglePermission, toProfile } from "../lib/staff";
import { staffSchema, type StaffFormValues } from "../schema";
import type { StaffAccount } from "../types";
import styles from "./staff.module.css";

/**
 * Thêm nhân viên (staff = undefined) hoặc sửa thông tin / quyền của nhân viên.
 * 3 nhóm tách bạch: Đăng nhập · Thông tin liên hệ (cột Ten, Di_Dong, Di_Dong2, Email, Email2 của dbo.TCustomer_NguoiLienLac) · Quyền sử dụng.
 */
export function StaffFormModal({
  staff,
  onClose,
}: {
  staff?: StaffAccount;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const isNew = !staff;
  const permissions = useAssignablePermissions();
  const create = useCreateStaff();
  const update = useUpdateStaff();
  const pending = create.isPending || update.isPending;

  const { register, control, handleSubmit, formState } =
    useForm<StaffFormValues>({
      resolver: zodResolver(staffSchema),
      defaultValues: {
        isNew,
        userName: "",
        password: "",
        confirmPassword: "",
        fullName: staff?.fullName ?? "",
        email: staff?.email ?? "",
        phone: staff?.phone ?? "",
        email2: staff?.email2 ?? "",
        phone2: staff?.phone2 ?? "",
        permissions: staff?.permissions ?? [],
      },
    });
  const err = (k: keyof StaffFormValues) => formState.errors[k]?.message;

  const submit = handleSubmit((values) => {
    const profile = toProfile(values);
    if (staff)
      update.mutate(
        { id: staff.id, body: { ...profile, active: staff.active } },
        { onSuccess: onClose },
      );
    else
      create.mutate(
        {
          ...profile,
          userName: values.userName.trim(),
          password: values.password,
        },
        { onSuccess: onClose },
      );
  });

  return (
    <Modal
      open
      size="lg"
      title={
        isNew
          ? "Thêm tài khoản nhân viên"
          : t("Sửa tài khoản {name}", { name: staff.userName })
      }
      onClose={onClose}
      footer={
        <>
          <Button onClick={onClose}>{t("Hủy")}</Button>
          <Button
            variant="primary"
            type="submit"
            form="staff-form"
            disabled={pending}
          >
            {t(
              pending ? "Đang lưu…" : isNew ? "Tạo tài khoản" : "Lưu thay đổi",
            )}
          </Button>
        </>
      }
    >
      <form
        id="staff-form"
        onSubmit={(e) => void submit(e)}
        noValidate
        className={styles.form}
      >
        <section className={styles.section}>
          <h3>1. {t("Đăng nhập")}</h3>
          {isNew ? (
            <>
              <Row
                label="Tên đăng nhập"
                required
                error={err("userName")}
                hint="Nhân viên dùng tên này để đăng nhập portal"
              >
                <input autoComplete="off" autoFocus {...register("userName")} />
              </Row>
              <Row
                label="Mật khẩu"
                required
                error={err("password") ?? err("confirmPassword")}
                hint="Tối thiểu 8 ký tự, gồm cả chữ và số"
              >
                <span className={styles.pair}>
                  <input
                    type="password"
                    autoComplete="new-password"
                    placeholder={t("Mật khẩu")}
                    aria-label={t("Mật khẩu")}
                    {...register("password")}
                  />
                  <input
                    type="password"
                    autoComplete="new-password"
                    placeholder={t("Nhập lại mật khẩu")}
                    aria-label={t("Nhập lại mật khẩu")}
                    {...register("confirmPassword")}
                  />
                </span>
              </Row>
            </>
          ) : (
            <Row
              label="Tên đăng nhập"
              hint="Không đổi được tên đăng nhập. Đổi mật khẩu bằng nút Đặt lại mật khẩu trong danh sách."
            >
              <input value={staff.userName} readOnly className="mono" />
            </Row>
          )}
        </section>

        <section className={styles.section}>
          <h3>2. {t("Thông tin liên hệ")}</h3>
          <Row label="Họ tên" required error={err("fullName")}>
            <input autoFocus={!isNew} {...register("fullName")} />
          </Row>
          <Row
            label="Di động"
            error={err("phone") ?? err("phone2")}
            hint="Số 1 điền sẵn SĐT người gửi khi nhân viên tạo đơn"
          >
            <span className={styles.pair}>
              <input
                type="tel"
                placeholder={t("Di động 1")}
                aria-label={t("Di động 1")}
                {...register("phone")}
              />
              <input
                type="tel"
                placeholder={t("Di động 2 (không bắt buộc)")}
                aria-label={t("Di động 2")}
                {...register("phone2")}
              />
            </span>
          </Row>
          <Row
            label="Email"
            error={err("email") ?? err("email2")}
            hint="Email 1 điền sẵn email người gửi khi nhân viên tạo đơn"
          >
            <span className={styles.pair}>
              <input
                type="email"
                placeholder={t("Email 1")}
                aria-label={t("Email 1")}
                {...register("email")}
              />
              <input
                type="email"
                placeholder={t("Email 2 (không bắt buộc)")}
                aria-label={t("Email 2")}
                {...register("email2")}
              />
            </span>
          </Row>
        </section>

        <section className={styles.section}>
          <h3>3. {t("Quyền sử dụng")}</h3>
          <p className={styles.hint}>
            {t(
              'Nhân viên chỉ thấy vận đơn và đơn E-commerce do mình tạo, trừ khi được chọn "Xem toàn bộ đơn" ở từng nhóm. Đơn tự đồng bộ từ sàn chỉ người có quyền xem toàn bộ đơn E-commerce thấy. MyTracking và quản lý nhân viên chỉ dành cho tài khoản chính.',
            )}
          </p>
          {permissions.isError ? (
            <Notice tone="danger">
              {t("Không tải được danh sách quyền. Vui lòng thử lại.")}
            </Notice>
          ) : permissions.isPending ? (
            <p className={styles.hint}>{t("Đang tải…")}</p>
          ) : (
            <Controller
              control={control}
              name="permissions"
              render={({ field }) => (
                <table className={styles.permTable}>
                  <tbody>
                    {groupPermissions(permissions.data).map((group) => (
                      <tr key={group.key}>
                        <th>{t(group.label)}</th>
                        <td>
                          <div className={styles.checks}>
                            {group.items.map((p) => (
                              <label key={p.code} className={styles.check}>
                                <input
                                  type="checkbox"
                                  checked={field.value.includes(p.code)}
                                  onChange={(e) =>
                                    field.onChange(
                                      togglePermission(
                                        field.value,
                                        p.code,
                                        e.target.checked,
                                      ),
                                    )
                                  }
                                />
                                <span>{t(p.description)}</span>
                              </label>
                            ))}
                          </div>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              )}
            />
          )}
        </section>
      </form>
    </Modal>
  );
}

/** 1 dòng form: nhãn bên trái, ô nhập bên phải, lỗi / gợi ý bên dưới ô. */
function Row({
  label,
  required,
  error,
  hint,
  children,
}: {
  label: string;
  required?: boolean;
  error?: string;
  hint?: string;
  children: ReactNode;
}) {
  const { t } = useI18n();
  return (
    <div className={styles.row}>
      <span className={styles.label}>
        {t(label)}
        {required && <i aria-hidden="true"> *</i>}
      </span>
      <span className={styles.control}>
        {children}
        {error ? (
          <small className={styles.error} role="alert">
            {t(error)}
          </small>
        ) : (
          hint && <small>{t(hint)}</small>
        )}
      </span>
    </div>
  );
}
