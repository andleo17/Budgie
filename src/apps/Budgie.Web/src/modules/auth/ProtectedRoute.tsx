import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '@/modules/auth/useAuth';
import { Layout } from '@/modules/layout/Layout';

export function ProtectedRoute() {
  const { token, isLoading } = useAuth();

  if (isLoading) {
    // Keep the app shell (sidebar + header) mounted so the first paint
    // matches the authenticated layout and nothing jumps.
    return <Layout loading />;
  }

  if (!token) {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
}
