import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { readAuthSession, writeAuthSession } from '../auth/authStorage';
import { request } from './client';

describe('API client authentication', () => {
  const fetchMock = vi.fn<typeof fetch>();

  beforeEach(() => {
    window.sessionStorage.clear();
    fetchMock.mockReset();
    vi.stubGlobal('fetch', fetchMock);
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('attaches the bearer token centrally', async () => {
    writeAuthSession({
      accessToken: 'signed-access-token',
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
    });
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ items: [] }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      }),
    );

    await request<{ items: unknown[] }>('/tickets');

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('/api/tickets');
    expect(new Headers(init?.headers).get('Authorization')).toBe(
      'Bearer signed-access-token',
    );
  });

  it('clears the stored session after any 401 response', async () => {
    writeAuthSession({
      accessToken: 'rejected-access-token',
      expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
    });
    fetchMock.mockResolvedValue(
      new Response(
        JSON.stringify({
          title: 'Unauthorized',
          detail: 'Authentication is required.',
          status: 401,
        }),
        {
          status: 401,
          headers: { 'Content-Type': 'application/json' },
        },
      ),
    );

    await expect(request('/tickets')).rejects.toMatchObject({ status: 401 });

    expect(readAuthSession()).toBeNull();
  });
});
