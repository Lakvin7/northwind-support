import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { authApi } from '../../api/auth';
import { ApiError } from '../../api/client';
import { AuthProvider } from '../../auth/AuthContext';
import { ProtectedRoute } from '../../auth/ProtectedRoute';
import { readAuthSession, writeAuthSession } from '../../auth/authStorage';
import { useAuth } from '../../auth/useAuth';
import type { AuthenticatedAgent, LoginResponse } from '../../types/api';
import { LoginPage } from './LoginPage';

vi.mock('../../api/auth', () => ({
  authApi: {
    login: vi.fn(),
    getCurrentAgent: vi.fn(),
  },
}));

const login = vi.mocked(authApi.login);
const getCurrentAgent = vi.mocked(authApi.getCurrentAgent);
const agent: AuthenticatedAgent = {
  id: 1,
  fullName: 'Alex Turner',
  email: 'alex.turner@northwind-support.example',
};

describe('authentication flow', () => {
  beforeEach(() => {
    window.sessionStorage.clear();
    vi.clearAllMocks();
  });

  it('redirects a protected route to login and preserves the destination', async () => {
    render(
      <AuthProvider>
        <MemoryRouter initialEntries={['/tickets/42?tab=history']}>
          <Routes>
            <Route element={<ProtectedRoute />}>
              <Route path="/tickets/:id" element={<p>Protected ticket</p>} />
            </Route>
            <Route path="/login" element={<LoginDestination />} />
          </Routes>
        </MemoryRouter>
      </AuthProvider>,
    );

    expect(await screen.findByText('Login destination')).toBeInTheDocument();
    expect(screen.getByText('/tickets/42?tab=history')).toBeInTheDocument();
    expect(screen.queryByText('Protected ticket')).not.toBeInTheDocument();
  });

  it('validates both login fields before sending credentials', async () => {
    const user = userEvent.setup();
    renderLogin();

    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(screen.getByText('Enter your email address.')).toBeInTheDocument();
    expect(screen.getByText('Enter your password.')).toBeInTheDocument();
    expect(login).not.toHaveBeenCalled();
  });

  it('shows the public error returned for rejected credentials', async () => {
    const user = userEvent.setup();
    login.mockRejectedValue(
      new ApiError(401, 'The email or password is incorrect.'),
    );
    renderLogin();

    await user.type(
      screen.getByRole('textbox', { name: 'Email' }),
      agent.email,
    );
    await user.type(screen.getByLabelText('Password'), 'incorrect-password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(
      await screen.findByText('The email or password is incorrect.'),
    ).toBeInTheDocument();
  });

  it('shows a pending state and returns to the intended route after login', async () => {
    const user = userEvent.setup();
    let resolveLogin!: (value: LoginResponse) => void;
    login.mockReturnValue(
      new Promise<LoginResponse>((resolve) => {
        resolveLogin = resolve;
      }),
    );

    renderLogin('/tickets/42');
    await user.type(
      screen.getByRole('textbox', { name: 'Email' }),
      agent.email,
    );
    await user.type(screen.getByLabelText('Password'), 'correct-password');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(screen.getByRole('button', { name: 'Signing in...' })).toBeDisabled();

    await act(async () => {
      resolveLogin({
        accessToken: 'signed-access-token',
        expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
        agent,
      });
    });

    expect(await screen.findByText('Requested ticket')).toBeInTheDocument();
    expect(readAuthSession()?.accessToken).toBe('signed-access-token');
  });

  it('restores the current agent and logout clears the session', async () => {
    const user = userEvent.setup();
    writeAuthSession({
      accessToken: 'stored-token',
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
    });
    getCurrentAgent.mockResolvedValue(agent);

    render(
      <AuthProvider>
        <SessionProbe />
      </AuthProvider>,
    );

    expect(await screen.findByText('Alex Turner')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Log out' }));

    expect(await screen.findByText('Signed out')).toBeInTheDocument();
    expect(readAuthSession()).toBeNull();
  });
});

function renderLogin(returnTo?: string) {
  render(
    <AuthProvider>
      <MemoryRouter
        initialEntries={[
          {
            pathname: '/login',
            state: returnTo ? { returnTo } : null,
          },
        ]}
      >
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/tickets/42" element={<p>Requested ticket</p>} />
          <Route path="/tickets" element={<p>Ticket list</p>} />
        </Routes>
      </MemoryRouter>
    </AuthProvider>,
  );
}

function LoginDestination() {
  const location = useLocation();
  const state = location.state as { returnTo?: string } | null;

  return (
    <>
      <p>Login destination</p>
      <p>{state?.returnTo}</p>
    </>
  );
}

function SessionProbe() {
  const { agent: currentAgent, isInitializing, logout } = useAuth();

  if (isInitializing) {
    return <p>Checking session</p>;
  }

  if (!currentAgent) {
    return <p>Signed out</p>;
  }

  return (
    <>
      <p>{currentAgent.fullName}</p>
      <button type="button" onClick={logout}>
        Log out
      </button>
    </>
  );
}
