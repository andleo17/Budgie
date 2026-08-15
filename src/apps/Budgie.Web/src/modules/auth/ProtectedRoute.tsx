import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '@/modules/auth/useAuth';
import { PageSkeleton } from '@/components/page-skeleton';

export function ProtectedRoute() {
  const { token, isLoading } = useAuth();

  if (isLoading) {
    return <PageSkeleton />;
  }

  if (!token) {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
}
