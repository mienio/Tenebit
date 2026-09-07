import { AlertTriangle, Plus } from 'lucide-react';
import { FormEvent, useEffect, useState } from 'react';
import { Button } from '../components/Button';
import { Field, TextArea, TextInput } from '../components/FormFields';
import { LoadingState } from '../components/StateViews';
import { PartnerLayout, PartnerPageHeader } from './PartnerLayout';
import {
  createMyMessageThread, getMyMessageThread, listMyMessageThreads, replyToMyMessageThread,
  type AffiliateMessageThread, type AffiliateMessageThreadSummary,
} from './partnerApi';

function NewThreadForm({ isComplaint, onDone, onCancel }: { isComplaint: boolean; onDone: (id: string) => void; onCancel: () => void }) {
  const [subject, setSubject] = useState(isComplaint ? '' : '');
  const [body, setBody] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const thread = await createMyMessageThread(subject.trim(), isComplaint, body.trim());
      onDone(thread.id);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się wysłać wiadomości.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="card" style={{ marginBottom: 16 }}>
      <h3 style={{ marginTop: 0 }}>{isComplaint ? 'Zgłoś zastrzeżenie' : 'Nowa wiadomość do Tenebit'}</h3>
      {isComplaint ? (
        <p className="adminMuted">
          Użyj tej opcji, jeśli nie zgadzasz się z decyzją dotyczącą Twojego konta, naliczonej prowizji lub wypłaty.
          Zgłoszenie trafia bezpośrednio do zespołu Tenebit - patrz punkt 9 <a href="/partner/terms" target="_blank" rel="noreferrer">regulaminu</a>.
        </p>
      ) : null}
      <form className="formGrid" onSubmit={handleSubmit}>
        <Field label="Temat">
          <TextInput value={subject} onChange={e => setSubject(e.target.value)} required minLength={3} maxLength={200} autoFocus />
        </Field>
        <Field label="Treść">
          <TextArea value={body} onChange={e => setBody(e.target.value)} rows={4} required maxLength={5000} />
        </Field>
        {error ? <p className="formMessage formMessage--error">{error}</p> : null}
        <div className="formActions">
          <Button type="button" variant="secondary" onClick={onCancel} disabled={submitting}>Anuluj</Button>
          <Button type="submit" variant={isComplaint ? 'danger' : 'primary'} disabled={submitting}>{submitting ? 'Wysyłanie…' : 'Wyślij'}</Button>
        </div>
      </form>
    </div>
  );
}

export function PartnerMessagesPage() {
  const [threads, setThreads] = useState<AffiliateMessageThreadSummary[] | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<AffiliateMessageThread | null>(null);
  const [composeMode, setComposeMode] = useState<'none' | 'question' | 'complaint'>('none');
  const [reply, setReply] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    listMyMessageThreads()
      .then(result => { setThreads(result); if (!selectedId && result.length > 0) setSelectedId(result[0].id); })
      .catch(err => setError(err instanceof Error ? err.message : 'Nie udało się pobrać wiadomości.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [reloadKey]);

  useEffect(() => {
    if (!selectedId) return;
    setDetail(null);
    getMyMessageThread(selectedId)
      .then(setDetail)
      .catch(err => setError(err instanceof Error ? err.message : 'Nie udało się pobrać wątku.'));
  }, [selectedId, reloadKey]);

  async function handleReply(event: FormEvent) {
    event.preventDefault();
    if (!selectedId || !reply.trim()) return;
    setSending(true);
    try {
      await replyToMyMessageThread(selectedId, reply.trim());
      setReply('');
      setReloadKey(k => k + 1);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się wysłać odpowiedzi.');
    } finally {
      setSending(false);
    }
  }

  return (
    <PartnerLayout>
      <PartnerPageHeader
        title="Wiadomości"
        description="Kontakt z zespołem Tenebit."
        actions={
          <>
            <Button variant="secondary" icon={<Plus size={16} />} onClick={() => setComposeMode('question')}>Nowa wiadomość</Button>
            <Button variant="danger" icon={<AlertTriangle size={16} />} onClick={() => setComposeMode('complaint')}>Zgłoś zastrzeżenie</Button>
          </>
        }
      />
      {error ? <p className="formMessage formMessage--error">{error}</p> : null}

      {composeMode !== 'none' && (
        <NewThreadForm
          isComplaint={composeMode === 'complaint'}
          onCancel={() => setComposeMode('none')}
          onDone={id => { setComposeMode('none'); setSelectedId(id); setReloadKey(k => k + 1); }}
        />
      )}

      {!threads ? <LoadingState /> : (
        <div style={{ display: 'grid', gridTemplateColumns: '280px 1fr', gap: 16 }}>
          <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
            {threads.map(t => (
              <button key={t.id} type="button" onClick={() => setSelectedId(t.id)}
                style={{
                  display: 'block', width: '100%', textAlign: 'left', padding: 12, border: 'none',
                  borderBottom: '1px solid var(--border, #e5e0d5)',
                  background: t.id === selectedId ? 'var(--surface-soft, #fdfbf6)' : 'transparent', cursor: 'pointer',
                }}>
                <strong>{t.subject}</strong>
                <div className="adminMuted">{t.category === 'Complaint' ? 'Zastrzeżenie' : 'Pytanie'} · {new Date(t.lastMessageAt).toLocaleDateString('pl-PL')}</div>
              </button>
            ))}
            {threads.length === 0 ? <p className="adminMuted" style={{ padding: 12 }}>Brak wiadomości.</p> : null}
          </div>
          <div className="card">
            {!detail ? <LoadingState /> : (
              <>
                <h3 style={{ marginTop: 0 }}>{detail.subject}</h3>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginBottom: 16 }}>
                  {detail.messages.map(m => (
                    <div key={m.id} style={{
                      alignSelf: m.senderType === 'Affiliate' ? 'flex-end' : 'flex-start',
                      background: m.senderType === 'Affiliate' ? 'var(--accent-soft, #f3e6d8)' : 'var(--surface-soft, #fdfbf6)',
                      padding: '8px 12px', borderRadius: 8, maxWidth: '80%',
                    }}>
                      <div className="adminMuted" style={{ marginBottom: 4 }}>{m.senderType === 'Affiliate' ? 'Ty' : 'Tenebit'} · {new Date(m.sentAt).toLocaleString('pl-PL')}</div>
                      <div style={{ whiteSpace: 'pre-wrap' }}>{m.body}</div>
                    </div>
                  ))}
                </div>
                <form onSubmit={handleReply} className="formGrid">
                  <TextArea value={reply} onChange={e => setReply(e.target.value)} rows={3} placeholder="Napisz odpowiedź…" maxLength={5000} />
                  <div className="formActions">
                    <Button type="submit" disabled={sending || !reply.trim()}>{sending ? 'Wysyłanie…' : 'Wyślij'}</Button>
                  </div>
                </form>
              </>
            )}
          </div>
        </div>
      )}
    </PartnerLayout>
  );
}
