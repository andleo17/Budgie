import { Suspense, useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { Loader2 } from 'lucide-react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { TooltipProvider } from '@/components/ui/tooltip';
import { AuthProvider } from '@/modules/auth/AuthProvider';
import { ProtectedRoute } from '@/modules/auth/ProtectedRoute';
import { Layout } from '@/modules/layout/Layout';
import { ThemeProvider } from '@/modules/settings/ThemeProvider';
import { LanguageProvider } from '@/i18n/LanguageProvider';
import { preloadRoutes } from '@/modules/routes';
import '@/i18n/config';

import { Toaster } from '@/components/ui/sonner';
import { Login, Register, Setup, Dashboard, Accounts, Transactions, Goals, Categories, Persons, TagManagement, Settings, Templates, Profile } from '@/modules/routes';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
  },
});

function App() {
  useEffect(() => {
    preloadRoutes();
  }, []);

  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <TooltipProvider>
        <ThemeProvider>
        <AuthProvider>
          <LanguageProvider>
          <Toaster />
          <Suspense fallback={<div className="flex h-screen items-center justify-center"><Loader2 className="h-6 w-6 animate-spin" /></div>}>
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route path="/register" element={<Register />} />

            <Route element={<ProtectedRoute />}>
              <Route path="/setup" element={<Setup />} />

              <Route element={<Layout />}>
                <Route path="/dashboard" element={<Dashboard />} />
                <Route path="/accounts" element={<Accounts />} />
                <Route path="/transactions" element={<Transactions />} />
                <Route path="/goals" element={<Goals />} />
                <Route path="/categories" element={<Categories />} />
                <Route path="/templates" element={<Templates />} />
                <Route path="/persons" element={<Persons />} />
                <Route path="/persons/tags" element={<TagManagement />} />
                <Route path="/settings" element={<Settings />} />
                <Route path="/profile" element={<Profile />} />
              </Route>

              <Route path="/" element={<Navigate to="/dashboard" replace />} />
            </Route>
          </Routes>
          </Suspense>
          </LanguageProvider>
        </AuthProvider>
        </ThemeProvider>
        </TooltipProvider>
      </BrowserRouter>
    </QueryClientProvider>
  );
}

export default App;
