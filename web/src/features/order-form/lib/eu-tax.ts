import { isEuCountry } from '@/shared/config/eu';

export const IOSS_MESSAGE = 'IOSS No phải có dạng IM và 10 chữ số';
export const EORI_MESSAGE = 'EORI No phải gồm 2 chữ cái mã nước và 1–15 chữ cái hoặc chữ số';

export const validateIossNo = (code: string | undefined, value: string | undefined): boolean =>
  !isEuCountry(code) || !value?.trim() || /^IM\d{10}$/.test(value.trim());

export const validateEoriNo = (code: string | undefined, value: string | undefined): boolean =>
  !isEuCountry(code) || !value?.trim() || /^[A-Z]{2}[A-Z0-9]{1,15}$/.test(value.trim());
