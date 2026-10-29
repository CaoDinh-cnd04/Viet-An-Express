/** API công khai của feature — feature khác chỉ import từ đây. */
/** Tra cứu địa lý (quốc gia, mã bưu chính qua GeoNames) — dùng chung cho form có địa chỉ người nhận. */
export { useCountries, usePostalSearch } from './api';
export { findCountry, normalizePostal, shouldResetAddress, type Country } from './lib/geo';
export { usePostalPlaceBox } from './hooks/usePostalPlaceBox';
