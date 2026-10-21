import { useCallback, useState } from 'react';
import { toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { ErrorMessage } from '../../../components/ErrorMessage';
import { Field } from '../../../components/Field';
import { useAsyncData } from '../../../hooks/useAsyncData';
import { formatDateTime } from '../../../lib/format';
import type { Agent, TicketDetail } from '../../../types/api';

const invalidReasonMessage =
  'Escalation reason must contain between 5 and 500 characters.';

interface EscalationPanelProps {
  ticket: TicketDetail;
  agents: Agent[];
  onEscalated: (ticket: TicketDetail) => Promise<void>;
}

/** Escalates a ticket and shows its immutable escalation history. */
export function EscalationPanel({ ticket, agents, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [reasonError, setReasonError] = useState<string>();
  const [actionError, setActionError] = useState<string>();
  const [isSaving, setIsSaving] = useState(false);

  const loadHistory = useCallback(
    () => ticketsApi.getEscalations(ticket.id),
    [ticket.id],
  );
  const history = useAsyncData(loadHistory, 'Unable to load escalation history.');

  const canEscalate =
    ticket.priority !== 'Critical' &&
    ticket.status !== 'Resolved' &&
    ticket.status !== 'Closed';

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    if (!canEscalate || isSaving || history.isLoading) {
      return;
    }

    const trimmedReason = reason.trim();
    if (trimmedReason.length < 5 || trimmedReason.length > 500) {
      setReasonError(invalidReasonMessage);
      return;
    }

    setReasonError(undefined);
    setActionError(undefined);
    setIsSaving(true);

    try {
      const updatedTicket = await ticketsApi.escalateTicket(ticket.id, trimmedReason);
      await Promise.all([
        onEscalated(updatedTicket),
        history.reload(),
      ]);
      setReason('');
    } catch (caught) {
      setActionError(toErrorMessage(caught, 'Could not escalate the ticket.'));
    } finally {
      setIsSaving(false);
    }
  };

  const agentName = (agentId: number | null) => {
    if (agentId === null) {
      return 'Unassigned';
    }

    return agents.find((agent) => agent.id === agentId)?.fullName ?? `Agent #${agentId}`;
  };

  return (
    <>
      <div className="card">
        <h2>Escalate ticket</h2>

        <form className="form" onSubmit={(event) => void submit(event)}>
          <Field
            id="escalation-reason"
            label="Reason"
            error={reasonError}
            hint="Enter between 5 and 500 characters."
          >
            {(fieldProps) => (
              <textarea
                {...fieldProps}
                rows={4}
                maxLength={500}
                value={reason}
                disabled={!canEscalate || isSaving || history.isLoading}
                onChange={(event) => {
                  setReason(event.target.value);
                  setReasonError(undefined);
                }}
              />
            )}
          </Field>

          <button
            type="submit"
            className="button button--primary"
            disabled={!canEscalate || isSaving || history.isLoading}
          >
            {isSaving ? 'Escalating...' : 'Escalate ticket'}
          </button>

          {!canEscalate && (
            <p className="field__hint">
              Resolved, closed, and Critical tickets cannot be escalated.
            </p>
          )}

          {actionError && (
            <p className="field__error" role="alert">
              {actionError}
            </p>
          )}
        </form>
      </div>

      <div className="card">
        <h2>Escalation history</h2>

        {history.isLoading && !history.data && (
          <p className="field__hint">Loading escalation history...</p>
        )}

        {history.error && (
          <ErrorMessage message={history.error} onRetry={() => void history.reload()} />
        )}

        {history.data?.length === 0 && (
          <p className="field__hint">No escalations recorded.</p>
        )}

        {history.data && history.data.length > 0 && (
          <div className="table-wrapper" aria-busy={history.isLoading}>
            <table className="table">
              <thead>
                <tr>
                  <th scope="col">Escalated</th>
                  <th scope="col">Priority</th>
                  <th scope="col">Assignment</th>
                  <th scope="col">SLA deadline</th>
                  <th scope="col">Reason</th>
                </tr>
              </thead>
              <tbody>
                {history.data.map((escalation) => (
                  <tr key={escalation.id}>
                    <td>{formatDateTime(escalation.escalatedAtUtc)}</td>
                    <td>
                      {escalation.fromPriority} to {escalation.toPriority}
                    </td>
                    <td>
                      {agentName(escalation.previousAgentId)} to {agentName(escalation.newAgentId)}
                    </td>
                    <td>
                      {formatDateTime(escalation.previousDueAtUtc)} to{' '}
                      {formatDateTime(escalation.newDueAtUtc)}
                    </td>
                    <td>{escalation.reason}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </>
  );
}
