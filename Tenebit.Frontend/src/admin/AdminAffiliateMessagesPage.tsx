import { FormEvent, useEffect, useState } from 'react';
import { Button } from '../components/Button';
import { TextArea } from '../components/FormFields';
import { LoadingState } from '../components/StateViews';
import { AdminPageHeader, AdminShell } from './AdminShell';
import {
  getAffiliateMessageThread,
  listAffiliateMessageThreads,
  replyToAffiliateMessageThread,
  type AffiliateMessageThread,
  type AffiliateMessageThreadSummary,
} from './adminApi';

export function AdminAffiliateMessagesPage() {
  const [threads, setThreads] = useState<AffiliateMessageThreadSummary[] | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [detail, setDetail] = useState<AffiliateMessageThread | null>(null);
  const [reply, setReply] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [sending, setSending] = useState(false);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let cancelled = false;
    listAffiliateMessageThreads()
      .then(result => {
        if (cancelled) return;
        setThreads(result);
        if (!selectedId && result.length > 0) setSelectedId(result[0].id);
      })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : 'Nie udało się pobrać wiadomości.'); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [reloadKey]);

  useEffect(() => {
    if (!selectedId) return;
    let cancelled = false;
    setDetail(null);
    getAffiliateMessageThread(selectedId)
      .then(result => { if (!cancelled) setDetail(result); })
      .catch(err => { if (!cancelled) setError(err instanceof Error ? err.message : 'Nie udało się pobrać wątku.'); });
    return () => { cancelled = true; };
  }, [selectedId, reloadKey]);

  async function handleReply(event: FormEvent) {
    event.preventDefault();
    if (!selectedId || !reply.trim()) return;
    setSending(true);
    setError(null);
    try {
      await replyToAffiliateMessageThread(selectedId, reply.trim());
      setReply('');
      setReloadKey(k => k + 1);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się wysłać odpowiedzi.');
    } finally {
      setSending(false);
    }
  }

  return (
    <AdminShell>
      <AdminPageHeader title="Wiadomości od partnerów" description="Pytania i zgłoszenia zastrzeżeń od afiliantów. Treść jest zawsze wyświetlana jako czysty tekst." />
      {error ? <p className="formMessage formMessage--error">{error}</p> : null}

      {!threads ? <LoadingState /> : (
        <div style={{ display: 'grid', gridTemplateColumns: '320px 1fr', gap: 16 }}>
          <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
            {threads.map(t => (
              <button
                key={t.id}
                type="button"
                onClick={() => setSelectedId(t.id)}
                style={{
                  display: 'block', width: '100%', textAlign: 'left', padding: 12, border: 'none',
                  borderBottom: '1px solid var(--border, #e5e0d5)',
                  background: t.id === selectedId ? 'var(--surface-soft, #fdfbf6)' : 'transparent',
                  cursor: 'pointer',
                }}
              >
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                  <strong>{t.subject}</strong>
                  {t.unreadByAdmin ? <span className="adminTag adminTag--danger">Nowa</span> : null}
                </div>
                <div className="adminMuted">
                  {t.category === 'Complaint' ? 'Zastrzeżenie' : 'Pytanie'} · {new Date(t.lastMessageAt).toLocaleString('pl-PL')}
                </div>
              </button>
            ))}
            {threads.length === 0 ? <p className="adminMuted" style={{ padding: 12 }}>Brak wiadomości.</p> : null}
          </div>

          <div className="card">
            {!detail ? <LoadingState /> : (
              <>
                <h3 style={{ marginTop: 0 }}>
                  {detail.subject} {detail.category === 'Complaint' ? <span className="adminTag adminTag--danger">Zastrzeżenie</span> : null}
                </h3>
                <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginBottom: 16 }}>
                  {detail.messages.map(m => (
                    <div key={m.id} style={{
                      alignSelf: m.senderType === 'Admin' ? 'flex-end' : 'flex-start',
                      background: m.senderType === 'Admin' ? 'var(--accent-soft, #f3e6d8)' : 'var(--surface-soft, #fdfbf6)',
                      padding: '8px 12px', borderRadius: 8, maxWidth: '80%',
                    }}>
                      <div className="adminMuted" style={{ marginBottom: 4 }}>
                        {m.senderType === 'Admin' ? 'Ty' : 'Partner'} · {new Date(m.sentAt).toLocaleString('pl-PL')}
                      </div>
                      <div style={{ whiteSpace: 'pre-wrap' }}>{m.body}</div>
                    </div>
                  ))}
                </div>
                <form onSubmit={handleReply} className="formGrid">
                  <TextArea value={reply} onChange={e => setReply(e.target.value)} rows={3} placeholder="Napisz odpowiedź…" maxLength={5000} />
                  <div className="formActions">
                    <Button type="submit" disabled={sending || !reply.trim()}>{sending ? 'Wysyłanie…' : 'Wyślij odpowiedź'}</Button>
                  </div>
                </form>
              </>
            )}
          </div>
        </div>
      )}
    </AdminShell>
  );
}
