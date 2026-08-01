import { Navigate, Outlet } from 'react-router-dom';
import { useAuth } from '@/modules/auth/useAuth';

export function ProtectedRoute() {
  const { token, isLoading } = useAuth();

  if (isLoading) {
    return <div className="flex h-screen items-center justify-center">Loading...</div>;
  }

  if (!token) {
    return <Navigate to="/login" replace />;
  }

  return <Outlet />;
}
