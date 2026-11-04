import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { getErrorMessage, isNotImplemented } from '@/shared/api/http';
import { useI18n } from '@/shared/i18n';
import { Button, FormGrid, Icon, Notice, TextAreaField, TextField, useToast, type IconName } from '@/shared/ui';
import { useSendContact } from '../api';
import { BRANCHES, COMPANY, CONTACTS } from '../constants';
import { contactSchema, type ContactRequest } from '../schema';
import { Section } from './Sections';
import styles from './ContactSection.module.css';

const INFO: ReadonlyArray<{ icon: IconName; label: string; value: string; href: string; external?: boolean }> = [
  { icon: 'phone', label: 'Hotline', value: CONTACTS.hotline.label, href: CONTACTS.hotline.href },
  { icon: 'phone', label: 'Tổng đài', value: CONTACTS.phone.label, href: CONTACTS.phone.href },
  { icon: 'mail', label: 'Email', value: CONTACTS.email.label, href: CONTACTS.email.href },
  { icon: 'mapPin', label: 'Trụ sở', value: COMPANY.address, href: COMPANY.mapUrl, external: true }
];

export function ContactSection() {
  const { t } = useI18n();
  return (
    <Section id="lien-he" eyebrow="Liên hệ" title="Cần báo giá hay tư vấn gửi hàng?" lead="Để lại lời nhắn, nhân viên Việt An sẽ gọi lại cho bạn trong giờ làm việc.">
      <div className={styles.layout}>
        <div className={styles.info}>
          {INFO.map(i => (
            <a key={i.label} href={i.href} className={styles.infoItem} {...(i.external && { target: '_blank', rel: 'noreferrer' })}>
              <span className={styles.infoIcon}>
                <Icon name={i.icon} size={20} />
              </span>
              <span>
                <span className={styles.infoLabel}>{t(i.label)}</span>
                <span className={styles.infoValue}>{t(i.value)}</span>
              </span>
            </a>
          ))}
          <div className={styles.branches}>
            <span className={styles.infoLabel}>{t('Chi nhánh')}</span>
            <ul>
              {BRANCHES.map(b => (
                <li key={b.city}>
                  <Icon name="mapPin" size={14} />
                  {t(b.city)}
                  {b.note && <span className={styles.note}>{t(b.note)}</span>}
                </li>
              ))}
            </ul>
          </div>
        </div>
        <ContactForm />
      </div>
    </Section>
  );
}

function ContactForm() {
  const { t } = useI18n();
  const send = useSendContact();
  const toast = useToast();
  const { register, handleSubmit, formState, reset } = useForm<ContactRequest>({
    resolver: zodResolver(contactSchema),
    defaultValues: { name: '', phone: '', email: '', message: '' }
  });
  const err = (k: keyof ContactRequest) => formState.errors[k]?.message;

  const submit = handleSubmit(values =>
    send.mutate(values, {
      onSuccess: res => {
        toast.show(res.message ?? 'Đã gửi lời nhắn, Việt An sẽ liên hệ lại sớm', 'success');
        reset();
      }
    })
  );

  return (
    <form className={styles.form} onSubmit={e => void submit(e)} noValidate>
      <h3 className={styles.formTitle}>{t('Gửi lời nhắn')}</h3>
      {send.isError &&
        (isNotImplemented(send.error) ? (
          <Notice title="Chức năng gửi lời nhắn đang được kết nối máy chủ">
            {t('Vui lòng gọi')} <a href={CONTACTS.hotline.href}>{CONTACTS.hotline.label}</a> {t('hoặc nhắn')}{' '}
            <a href={CONTACTS.zalo.href} target="_blank" rel="noreferrer">
              Zalo
            </a>
            .
          </Notice>
        ) : (
          <Notice tone="danger">{getErrorMessage(send.error, 'Gửi không thành công, vui lòng thử lại')}</Notice>
        ))}
      <FormGrid>
        <TextField label="Họ tên" required autoComplete="name" error={err('name')} {...register('name')} />
        <TextField label="Số điện thoại" required type="tel" autoComplete="tel" error={err('phone')} {...register('phone')} />
      </FormGrid>
      <TextField label="Email" type="email" autoComplete="email" error={err('email')} {...register('email')} />
      <TextAreaField
        label="Nội dung"
        required
        rows={4}
        placeholder="VD: Gửi 15 kg quần áo đi Úc, cần báo giá và lấy hàng tại Quận 7"
        error={err('message')}
        {...register('message')}
      />
      <Button variant="primary" type="submit" className={styles.submit} disabled={send.isPending}>
        
        {t(send.isPending ? 'Đang gửi…' : 'Gửi lời nhắn')}
      </Button>
    </form>
  );
}
