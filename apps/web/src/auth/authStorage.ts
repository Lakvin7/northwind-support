const storageKey = 'northwind-support.auth';
const sessionClearedEvent = 'northwind-support:session-cleared';

export interface StoredAuthSession {
  accessToken: string;
  expiresAtUtc: string;
}

/** Reads the current browser session, discarding malformed or expired values. */
export function readAuthSession(): StoredAuthSession | null {
  try {
    const serialized = window.sessionStorage.getItem(storageKey);
    if (!serialized) {
      return null;
    }

    const candidate = JSON.parse(serialized) as unknown;
    if (!isStoredAuthSession(candidate) || isExpired(candidate.expiresAtUtc)) {
      clearAuthSession();
      return null;
    }

    return candidate;
  } catch {
    clearAuthSession();
    return null;
  }
}

/** Persists a token only for the lifetime of the current browser tab. */
export function writeAuthSession(session: StoredAuthSession): void {
  window.sessionStorage.setItem(storageKey, JSON.stringify(session));
}

/** Removes credentials and tells the React authentication state to update. */
export function clearAuthSession(): void {
  try {
    window.sessionStorage.removeItem(storageKey);
  } finally {
    window.dispatchEvent(new Event(sessionClearedEvent));
  }
}

export function getAccessToken(): string | undefined {
  return readAuthSession()?.accessToken;
}

export function onAuthSessionCleared(listener: () => void): () => void {
  window.addEventListener(sessionClearedEvent, listener);
  return () => window.removeEventListener(sessionClearedEvent, listener);
}

function isStoredAuthSession(value: unknown): value is StoredAuthSession {
  if (typeof value !== 'object' || value === null) {
    return false;
  }

  const session = value as Record<string, unknown>;
  return (
    typeof session.accessToken === 'string' &&
    session.accessToken.length > 0 &&
    typeof session.expiresAtUtc === 'string' &&
    Number.isFinite(Date.parse(session.expiresAtUtc))
  );
}

function isExpired(expiresAtUtc: string): boolean {
  return Date.parse(expiresAtUtc) <= Date.now();
}
