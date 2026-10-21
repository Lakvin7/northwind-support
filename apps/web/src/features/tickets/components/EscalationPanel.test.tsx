import { render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { ticketsApi } from '../../../api/tickets';
import type { TicketDetail } from '../../../types/api';
import { EscalationPanel } from './EscalationPanel';

vi.mock('../../../api/tickets', () => ({
  ticketsApi: {
    getEscalations: vi.fn(),
    escalateTicket: vi.fn(),
  },
}));

const getEscalations = vi.mocked(ticketsApi.getEscalations);

describe('EscalationPanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getEscalations.mockResolvedValue([
      {
        id: 7,
        ticketId: 41,
        fromPriority: 'Medium',
        toPriority: 'High',
        reason: 'Customer impact increased.',
        escalatedBy: 'Alex Turner',
        escalatedAtUtc: '2026-10-21T09:00:00Z',
        previousDueAtUtc: '2026-10-22T09:00:00Z',
        newDueAtUtc: '2026-10-21T17:00:00Z',
        previousAgentId: null,
        newAgentId: 1,
      },
    ]);
  });

  it('shows the authenticated escalation actor in history', async () => {
    render(
      <EscalationPanel
        ticket={ticket}
        agents={[
          {
            id: 1,
            fullName: 'Alex Turner',
            email: 'alex.turner@northwind-support.example',
            isActive: true,
            maxOpenTickets: 10,
            openTicketCount: 2,
            specializations: [],
          },
        ]}
        onEscalated={vi.fn()}
      />,
    );

    expect(await screen.findByText('Alex Turner')).toBeInTheDocument();
    expect(screen.getByText('Customer impact increased.')).toBeInTheDocument();
  });
});

const ticket: TicketDetail = {
  id: 41,
  reference: 'TCK-0041',
  title: 'Customer login problem',
  description: 'Customers cannot access their accounts.',
  status: 'Open',
  priority: 'High',
  customer: {
    id: 1,
    name: 'Contoso Ltd',
    email: 'support@contoso.example',
    phone: null,
    tier: 'Standard',
  },
  category: {
    id: 1,
    name: 'General',
    requiresSpecialist: false,
  },
  assignedAgent: { id: 1, fullName: 'Alex Turner' },
  createdAtUtc: '2026-10-21T08:00:00Z',
  updatedAtUtc: '2026-10-21T09:00:00Z',
  dueAtUtc: '2026-10-21T17:00:00Z',
  resolvedAtUtc: null,
  slaStatus: 'WithinSla',
};
