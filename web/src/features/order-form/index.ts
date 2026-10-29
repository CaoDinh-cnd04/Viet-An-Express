/** Mô hình form đơn hàng (schema, hằng số, quy tắc tính cân / invoice / thuế EU) — feature nền, chỉ phụ thuộc shared/. */
export { EXPORT_TYPES, MULTI_CATEGORY, RULES } from './constants';
export { hasMaxDecimals } from './lib/decimal';
export { copyPreviousPackage, packageCount, packageGroups, syncPackageItems, writeInvoiceItems } from './lib/package-invoice';
export { applyDocWeightRule, buildDraftPayload, invoiceTotal, lineTotal, packageChargeableWeight, summarizePackages, toNumber, volumetricWeight } from './lib/shipment';
export { OTHER_UNIT, PRESET_UNITS, UNIT_MAX, normalizeUnit, qtyError, unitChoice } from './lib/units';
export { STEP_FIELDS, createOrderSchema, defaultValues, emptyInvoiceItem, emptyPackage } from './schema';
export type { CreateOrderValues, InvoiceItemValues, PackageValues } from './schema';
