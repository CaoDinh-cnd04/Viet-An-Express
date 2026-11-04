/**
 * Dịch vụ – hub đi được tới nước đến nào (mã ISO-2), "*" = mọi nước. Nguồn: file CS điền bằng công cụ tools/bang-hub-nuoc-den.html
 * (Bang_hub_nuoc_den_2026-11-04.xlsx; Chuyên tuyến / SEA là bản soạn theo tên hub, hub VA đi mọi nước — người dùng chốt 04/11/2026).
 * Cập nhật: CS điền lại công cụ → tải Excel → chép vào đây.
 */
export const HUB_COUNTRIES: Readonly<Record<string, readonly string[] | '*'>> = {
  'Chuyên tuyến|Sin (Đông Lạnh)': ['SG'],
  'Chuyên tuyến|Singapore': ['SG'],
  'Chuyên tuyến|UK': ['GB'],
  'Chuyên tuyến|AU_AuPost': ['AU'],
  'Chuyên tuyến|AU_Toll (T.Phẩm/M.Phẩm)': ['AU'],
  'Chuyên tuyến|AU_Toll Vip (Hàng thường)': ['AU'],
  'Chuyên tuyến|Canada': ['CA'],
  'Chuyên tuyến|Dubai': ['AE'],
  'Chuyên tuyến|Indonesia': ['ID'],
  'Chuyên tuyến|Korea': ['KR'],
  'Chuyên tuyến|Laos': ['LA'],
  'Chuyên tuyến|Saudi': ['SA'],
  'Chuyên tuyến|USA': ['US'],
  'Chuyên tuyến|USA - Sea': ['US'],
  'Chuyên tuyến|EU': ['AT', 'BE', 'BG', 'CY', 'CZ', 'DE', 'DK', 'EE', 'ES', 'FI', 'FR', 'GR', 'HR', 'HU', 'IE', 'IT', 'LT', 'LU', 'LV', 'MT', 'NL', 'PL', 'PT', 'RO', 'SE', 'SI', 'SK'],
  'Chuyên tuyến|Japan': ['JP'],
  'Chuyên tuyến|Malaysia': ['MY'],
  'Chuyên tuyến|China': ['CN'],
  'Chuyên tuyến|India': ['IN'],
  'Chuyên tuyến|Russia': ['RU'],
  'Chuyên tuyến|Taiwan': ['TW'],
  'Chuyên tuyến|Hong Kong': ['HK'],
  'Chuyên tuyến|Thailand - AIR': ['TH'],
  'Chuyên tuyến|Thailand - Truck': ['TH'],
  'Chuyên tuyến|Cambodia': ['KH'],
  'Chuyên tuyến|Philippines': ['PH'],
  'Chuyên tuyến|VA': '*',
  'SEA|Australia': ['AU'],
  'SEA|Dubai': ['AE'],
  'SEA|Indonesia': ['ID'],
  'SEA|Malaysia': ['MY'],
  'SEA|Philippine': ['PH'],
  'SEA|Singapore': ['SG'],
  'SEA|USA': ['US'],
  'DHL|VN': '*',
  'DHL|SIN VIP': '*',
  'DHL|Singapore': '*',
  'DHL|UK': '*',
  'DHL|Dubai': '*',
  'Fedex|SIN 1': '*',
  'Fedex|SIN 2': '*',
  'Fedex|SIN 3 (FICP)': '*',
  'Fedex|Singapore': '*',
  'Fedex|UK': '*',
  'Fedex|VN': '*',
  'UPS|Dubai': ['AM', 'AT', 'AZ', 'BE', 'BG', 'BH', 'CY', 'CZ', 'DE', 'DK', 'EE', 'ES', 'FI', 'FR', 'GE', 'GR', 'HR', 'HU', 'IE', 'IL', 'IQ', 'IR', 'IT', 'JO', 'KG', 'KW', 'KZ', 'LB', 'LT', 'LU', 'LV', 'MT', 'NL', 'OM', 'PL', 'PS', 'PT', 'QA', 'RO', 'SA', 'SE', 'SI', 'SK', 'SY', 'TJ', 'TM', 'TR', 'UZ', 'YE'],
  'UPS|Singapore': '*',
  'UPS|UK': '*',
  'UPS|VN Saver': '*',
  'UPS|TaiWan': '*',
  'UPS|VN Expedited': '*',
  'Aramex|Dubai': ['AE', 'AM', 'AO', 'AZ', 'BF', 'BH', 'BI', 'BJ', 'BW', 'CD', 'CF', 'CG', 'CI', 'CM', 'CV', 'DJ', 'DZ', 'EG', 'EH', 'ER', 'ET', 'GA', 'GE', 'GH', 'GM', 'GN', 'GQ', 'GW', 'IL', 'IQ', 'IR', 'JO', 'KE', 'KG', 'KM', 'KW', 'KZ', 'LB', 'LR', 'LS', 'LY', 'MA', 'MG', 'ML', 'MR', 'MU', 'MW', 'MZ', 'NA', 'NE', 'NG', 'OM', 'PS', 'QA', 'RE', 'RW', 'SA', 'SC', 'SD', 'SH', 'SL', 'SN', 'SO', 'ST', 'SY', 'SZ', 'TD', 'TG', 'TJ', 'TM', 'TN', 'TR', 'TZ', 'UG', 'UZ', 'YE', 'YT', 'ZA', 'ZM', 'ZW'],
  'Ecommerce|SING POST': '*',
  'Ecommerce|Việt An': '*',
  'Ủy quyền Việt An|Tự chọn': '*',
};

/**
 * Nước hãng KHÔNG / NGƯNG / TẠM NGƯNG phục vụ (file "Danh sách quốc gia KHÔNG, NGƯNG PHỤC VỤ DHL UPS FEDEX WORLDWIDE", 04/11/2026):
 * ẩn mọi hub của hãng với các nước này, kể cả hub "đi mọi nước". UPS "đình chỉ một số dịch vụ" cũng ẩn hẳn (người dùng chốt).
 * Fedex: Johnston / Wake Island = UM.
 */
export const CARRIER_EXCLUDED: Readonly<Record<string, readonly string[]>> = {
  DHL: ['AC', 'BV', 'IO', 'TF', 'HM', 'PN', 'PM', 'TJ', 'TA', 'UM', 'EH'],
  Fedex: ['KM', 'CU', 'FK', 'IR', 'KI', 'KP', 'YT', 'MM', 'NR', 'NU', 'SH', 'PM', 'ST', 'SB', 'SO', 'SD', 'TJ', 'TK', 'TV', 'UM', 'CF', 'GQ', 'GW', 'SL', 'SY', 'YE', 'RU', 'BY'],
  UPS: ['AS', 'BT', 'CF', 'CK', 'FM', 'FO', 'GL', 'KH', 'KI', 'LA', 'MH', 'YT', 'NC', 'PG', 'PW', 'SB', 'TL', 'TM', 'TO', 'TV', 'VU', 'WF', 'WS', 'UA', 'RU', 'BY'],
};
