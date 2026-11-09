/** API công khai của feature — feature khác chỉ import từ đây. */
export { useActAsCustomer, useActableCustomers, useCan, useLogout, useSession } from './api';
export { ActingBanner } from './components/ActingBanner';
export { AssignManagerButton } from './components/AssignManagerButton';
export { IdleLogout } from './components/IdleLogout';
export { onBeforeIdleLogout } from './lib/idle';
export { LoginForm } from './components/LoginForm';
export { RequireAuth } from './components/RequireAuth';
export { RequirePermission } from './components/RequirePermission';
export { can, PERMISSIONS, type Permission } from './lib/permissions';
export { CHOOSE_CUSTOMER_PATH, initialsOf, isVietAnAgent, MANAGED_ORDERS_PATH, PORTAL_HOME } from './lib/session';
export type { AccountType, ActableCustomer, Session, SessionUser } from './types';
