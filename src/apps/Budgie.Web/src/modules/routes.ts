import { lazy } from 'react';

export const Login = lazy(() => import('@/modules/auth/pages/Login'));
export const Register = lazy(() => import('@/modules/auth/pages/Register'));
export const Setup = lazy(() => import('@/modules/auth/pages/Setup'));
export const Dashboard = lazy(() => import('@/modules/dashboard/pages/Dashboard'));
export const Accounts = lazy(() => import('@/modules/accounts/pages/Accounts'));
export const Transactions = lazy(() => import('@/modules/transactions/pages/Transactions'));
export const Goals = lazy(() => import('@/modules/goals/pages/Goals'));
export const Categories = lazy(() => import('@/modules/categories/pages/Categories'));
export const Persons = lazy(() => import('@/modules/persons/pages/Persons'));
export const TagManagement = lazy(() => import('@/modules/persons/pages/TagManagement'));
export const Settings = lazy(() => import('@/modules/settings/pages/Settings'));
export const Templates = lazy(() => import('@/modules/transactions/pages/Templates'));
export const Profile = lazy(() => import('@/modules/profile/pages/Profile'));
export const Import = lazy(() => import('@/modules/import/pages/ImportPage'));
export const EmailImportInbox = lazy(() => import('@/modules/email-import/pages/EmailImportInbox'));

const lazyModules = [
  () => import('@/modules/auth/pages/Login'),
  () => import('@/modules/auth/pages/Register'),
  () => import('@/modules/auth/pages/Setup'),
  () => import('@/modules/dashboard/pages/Dashboard'),
  () => import('@/modules/accounts/pages/Accounts'),
  () => import('@/modules/transactions/pages/Transactions'),
  () => import('@/modules/goals/pages/Goals'),
  () => import('@/modules/categories/pages/Categories'),
  () => import('@/modules/persons/pages/Persons'),
  () => import('@/modules/persons/pages/TagManagement'),
  () => import('@/modules/settings/pages/Settings'),
  () => import('@/modules/transactions/pages/Templates'),
  () => import('@/modules/profile/pages/Profile'),
  () => import('@/modules/import/pages/ImportPage'),
  () => import('@/modules/email-import/pages/EmailImportInbox'),
];

let preloaded = false;

/** Preloads all lazy route chunks in the background to avoid page-flash on navigation. */
export function preloadRoutes(): void {
  if (preloaded || typeof window === 'undefined') return;
  preloaded = true;
  if ('requestIdleCallback' in window) {
    (window as Window & { requestIdleCallback: (cb: () => void) => number }).requestIdleCallback(() => {
      for (const load of lazyModules) load().catch(() => {});
    });
  } else {
    for (const load of lazyModules) load().catch(() => {});
  }
}
