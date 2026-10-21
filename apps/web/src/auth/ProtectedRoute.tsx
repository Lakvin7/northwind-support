import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { Spinner } from '../components/Spinner';
import { useAuth } from './useAuth';

export function ProtectedRoute() {
  const { agent, isInitializing } = useAuth();
  const location = useLocation();

  if (isInitializing) {
    return (
      <main className="app__main">
        <Spinner label="Checking session" />
      </main>
    );
  }

  if (!agent) {
    const returnTo = `${location.pathname}${location.search}${location.hash}`;
    return <Navigate to="/login" replace state={{ returnTo }} />;
  }

  return <Outlet />;
}
