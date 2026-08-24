import { ShieldAlert } from 'lucide-react';
import { FormEvent, useState } from 'react';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';

export interface AdminActionRequest {
  title: string;
  description: string;
  confirmLabel: string;
  requiresReason: boolean;
  run: (reason: string, totpCode: string) => Promise<void>;
}

/**
 * The single confirmation surface for every state-changing admin action.
 *
 * It always asks for a current 2FA code because the server enforces step-up authentication on these
 * endpoints: a valid session alone is not enough to change anyone's access, so possession of the
 * authenticator is re-proved per action rather than once per login.
 */
export function AdminActionDialog({ request, onClose, onDone }: { request: AdminActionRequest | null; onClose: () => void; onDone: () => void }) {
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  if (!request) return null;

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!request) return;
    const form = new FormData(event.currentTarget);
    setError(null);
    setSubmitting(true);
    try {
      await request.run(String(form.get('reason') ?? ''), String(form.get('totpCode') ?? ''));
      onDone();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Operacja nie powiodła się.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="adminDialog" role="dialog" aria-modal="true" aria-label={request.title}>
      <div className="adminDialog__panel">
        <div className="adminDialog__head">
          <ShieldAlert size={20} />
          <h2>{request.title}</h2>
        </div>
        <p className="adminDialog__text">{request.description}</p>
        <form className="formGrid" onSubmit={handleSubmit}>
          {request.requiresReason ? (
            <Field label="Powód" info="Zapisywany w dzienniku administratora">
              <TextInput name="reason" required minLength={3} maxLength={500} autoFocus />
            </Field>
          ) : null}
          <Field label="Kod 2FA" info="Aktualny kod z aplikacji uwierzytelniającej — wymagany do każdej operacji">
            <TextInput name="totpCode" inputMode="numeric" maxLength={6} minLength={6} required autoComplete="one-time-code" autoFocus={!request.requiresReason} />
          </Field>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <div className="adminDialog__actions">
            <Button type="button" variant="secondary" onClick={onClose} disabled={submitting}>Anuluj</Button>
            <Button variant="danger" disabled={submitting}>{submitting ? 'Wykonuję…' : request.confirmLabel}</Button>
          </div>
        </form>
      </div>
    </div>
  );
}
