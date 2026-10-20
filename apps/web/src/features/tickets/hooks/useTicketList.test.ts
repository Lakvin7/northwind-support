import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import { page, ticketListItem } from '../../../test/fixtures';
import { useTicketList } from './useTicketList';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getTickets: vi.fn(),
  },
}));

const getTickets = vi.mocked(ticketsApi.getTickets);

describe('useTicketList', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    getTickets.mockResolvedValue(page([ticketListItem()]));
  });

  afterEach(() => {
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  it('requests updated tickets when the status filter changes', async () => {
    const { result } = renderHook(() => useTicketList());

    //Wait for the initial request.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    //Change the status while remaining in page 1
    act(() => {
      result.current.updateFilters({ status: 'New' });
    });

    //Allow any scheduled work to run
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    //A new API request must be sent with the selected status.
    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ status: 'New', page: 1 }),
    );
  });

  it('ignores an older API response when a newer request finishes first', async () => {
    let resolveOldRequest!: (value: ReturnType<typeof page>) => void;

    const oldRequest = new Promise<ReturnType<typeof page>>((resolve) => {
      resolveOldRequest = resolve;
    });

    getTickets
      .mockImplementationOnce(() => oldRequest)
      .mockResolvedValueOnce(page([]));

    const { result } = renderHook(() => useTicketList());

    // Start the initial request.
    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    // Trigger a second request.
    act(() => {
      result.current.updateFilters({ status: 'New' });
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(result.current.data?.items).toHaveLength(0);

    // The older request finishes after the newer one.
    await act(async () => {
      resolveOldRequest(page([ticketListItem()]));
    });

    // The old response must not replace the newer results.
    expect(result.current.data?.items).toHaveLength(0);
  });

  it('searches with the text the user typed, once the typing settles', async () => {
    const { result } = renderHook(() => useTicketList());

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(getTickets).toHaveBeenCalledTimes(1);

    act(() => {
      result.current.updateFilters({ search: 'invoice' });
    });

    // Still inside the debounce window: no second request yet.
    expect(getTickets).toHaveBeenCalledTimes(1);

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(0);
    });

    expect(getTickets).toHaveBeenCalledTimes(2);
    expect(getTickets).toHaveBeenLastCalledWith(
      expect.objectContaining({ search: 'invoice', page: 1 }),
    );

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data?.items).toHaveLength(1);
  });
});
