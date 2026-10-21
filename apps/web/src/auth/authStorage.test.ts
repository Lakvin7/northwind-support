import { beforeEach, describe, expect, it, vi } from 'vitest';
import {
  clearAuthSession,
  getAccessToken,
  onAuthSessionCleared,
  readAuthSession,
  writeAuthSession,
} from './authStorage';

describe('authStorage', () => {
  beforeEach(() => {
    window.sessionStorage.clear();
  });

  it('stores a valid token for the current browser tab', () => {
    const session = {
      accessToken: 'signed-token',
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
    };

    writeAuthSession(session);

    expect(readAuthSession()).toEqual(session);
    expect(getAccessToken()).toBe('signed-token');
  });

  it('discards an expired token', () => {
    writeAuthSession({
      accessToken: 'expired-token',
      expiresAtUtc: new Date(Date.now() - 1_000).toISOString(),
    });

    expect(readAuthSession()).toBeNull();
    expect(window.sessionStorage).toHaveLength(0);
  });

  it('notifies authentication state when the session is cleared', () => {
    const listener = vi.fn();
    const unsubscribe = onAuthSessionCleared(listener);

    clearAuthSession();

    expect(listener).toHaveBeenCalledOnce();
    unsubscribe();
  });
});
