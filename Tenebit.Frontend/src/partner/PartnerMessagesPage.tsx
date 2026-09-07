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
import { usePartnerLocale, type PartnerLocale } from './i18n';

const content: Record<PartnerLocale, {
  title: string; description: string;
  newMessageAction: string; complaintAction: string;
  fetchThreadsError: string; fetchThreadError: string; sendError: string; replyError: string;
  complaintTitle: string; newMessageTitle: string; complaintInfo: (termsHref: string) => JSX.Element;
  subject: string; body: string; cancel: string; send: string; sending: string;
  complaintCategory: string; questionCategory: string; you: string; tenebit: string;
  noThreads: string; replyPlaceholder: string; dateLocale: string;
}> = {
  en: {
    title: 'Messages', description: 'Contact with the Tenebit team.',
    newMessageAction: 'New message', complaintAction: 'Report a concern',
    fetchThreadsError: 'Could not fetch messages.', fetchThreadError: 'Could not fetch the thread.',
    sendError: 'Could not send the message.', replyError: 'Could not send the reply.',
    complaintTitle: 'Report a concern', newMessageTitle: 'New message to Tenebit',
    complaintInfo: termsHref => (
      <>
        Use this option if you disagree with a decision about your account, an accrued commission, or
        a payout. The report goes directly to the Tenebit team - see point 9 of the <a href={termsHref} target="_blank" rel="noreferrer">terms</a>.
      </>
    ),
    subject: 'Subject', body: 'Message', cancel: 'Cancel', send: 'Send', sending: 'Sending…',
    complaintCategory: 'Concern', questionCategory: 'Question', you: 'You', tenebit: 'Tenebit',
    noThreads: 'No messages yet.', replyPlaceholder: 'Write a reply…', dateLocale: 'en-GB',
  },
  pl: {
    title: 'Wiadomości', description: 'Kontakt z zespołem Tenebit.',
    newMessageAction: 'Nowa wiadomość', complaintAction: 'Zgłoś zastrzeżenie',
    fetchThreadsError: 'Nie udało się pobrać wiadomości.', fetchThreadError: 'Nie udało się pobrać wątku.',
    sendError: 'Nie udało się wysłać wiadomości.', replyError: 'Nie udało się wysłać odpowiedzi.',
    complaintTitle: 'Zgłoś zastrzeżenie', newMessageTitle: 'Nowa wiadomość do Tenebit',
    complaintInfo: termsHref => (
      <>
        Użyj tej opcji, jeśli nie zgadzasz się z decyzją dotyczącą Twojego konta, naliczonej prowizji lub
        wypłaty. Zgłoszenie trafia bezpośrednio do zespołu Tenebit - patrz punkt 9 <a href={termsHref} target="_blank" rel="noreferrer">regulaminu</a>.
      </>
    ),
    subject: 'Temat', body: 'Treść', cancel: 'Anuluj', send: 'Wyślij', sending: 'Wysyłanie…',
    complaintCategory: 'Zastrzeżenie', questionCategory: 'Pytanie', you: 'Ty', tenebit: 'Tenebit',
    noThreads: 'Brak wiadomości.', replyPlaceholder: 'Napisz odpowiedź…', dateLocale: 'pl-PL',
  },
};

function NewThreadForm({ isComplaint, onDone, onCancel, t, termsHref }: {
  isComplaint: boolean; onDone: (id: string) => void; onCancel: () => void;
  t: typeof content['en']; termsHref: string;
}) {
  const [subject, setSubject] = useState('');
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
      setError(err instanceof Error ? err.message : t.sendError);
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="card" style={{ marginBottom: 16 }}>
      <h3 style={{ marginTop: 0 }}>{isComplaint ? t.complaintTitle : t.newMessageTitle}</h3>
      {isComplaint ? (
        <p className="adminMuted">{t.complaintInfo(termsHref)}</p>
      ) : null}
      <form className="formGrid" onSubmit={handleSubmit}>
        <Field label={t.subject}>
          <TextInput value={subject} onChange={e => setSubject(e.target.value)} required minLength={3} maxLength={200} autoFocus />
        </Field>
        <Field label={t.body}>
          <TextArea value={body} onChange={e => setBody(e.target.value)} rows={4} required maxLength={5000} />
        </Field>
        {error ? <p className="formMessage formMessage--error">{error}</p> : null}
        <div className="formActions">
          <Button type="button" variant="secondary" onClick={onCancel} disabled={submitting}>{t.cancel}</Button>
          <Button type="submit" variant={isComplaint ? 'danger' : 'primary'} disabled={submitting}>{submitting ? t.sending : t.send}</Button>
        </div>
      </form>
    </div>
  );
}

export function PartnerMessagesPage() {
  const { locale, path } = usePartnerLocale();
  const t = content[locale];
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
      .catch(err => setError(err instanceof Error ? err.message : t.fetchThreadsError));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [reloadKey]);

  useEffect(() => {
    if (!selectedId) return;
    setDetail(null);
    getMyMessageThread(selectedId)
      .then(setDetail)
      .catch(err => setError(err instanceof Error ? err.message : t.fetchThreadError));
    // eslint-disable-next-line react-hooks/exhaustive-deps
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
      setError(err instanceof Error ? err.message : t.replyError);
    } finally {
      setSending(false);
    }
  }

  return (
    <PartnerLayout>
      <PartnerPageHeader
        title={t.title}
        description={t.description}
        actions={
          <>
            <Button variant="secondary" icon={<Plus size={16} />} onClick={() => setComposeMode('question')}>{t.newMessageAction}</Button>
            <Button variant="danger" icon={<AlertTriangle size={16} />} onClick={() => setComposeMode('complaint')}>{t.complaintAction}</Button>
          </>
        }
      />
      {error ? <p className="formMessage formMessage--error">{error}</p> : null}

      {composeMode !== 'none' && (
        <NewThreadForm
          isComplaint={composeMode === 'complaint'}
          onCancel={() => setComposeMode('none')}
          onDone={id => { setComposeMode('none'); setSelectedId(id); setReloadKey(k => k + 1); }}
          t={t}
          termsHref={path('terms')}
        />
      )}

      {!threads ? <LoadingState /> : (
        <div style={{ display: 'grid', gridTemplateColumns: '280px 1fr', gap: 16 }}>
          <div className="card" style={{ padding: 0, overflow: 'hidden' }}>
            {threads.map(thread => (
              <button key={thread.id} type="button" onClick={() => setSelectedId(thread.id)}
                style={{
                  display: 'block', width: '100%', textAlign: 'left', padding: 12, border: 'none',
                  borderBottom: '1px solid var(--border, #e5e0d5)',
                  background: thread.id === selectedId ? 'var(--surface-soft, #fdfbf6)' : 'transparent', cursor: 'pointer',
                }}>
                <strong>{thread.subject}</strong>
                <div className="adminMuted">{thread.category === 'Complaint' ? t.complaintCategory : t.questionCategory} · {new Date(thread.lastMessageAt).toLocaleDateString(t.dateLocale)}</div>
              </button>
            ))}
            {threads.length === 0 ? <p className="adminMuted" style={{ padding: 12 }}>{t.noThreads}</p> : null}
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
                      <div className="adminMuted" style={{ marginBottom: 4 }}>{m.senderType === 'Affiliate' ? t.you : t.tenebit} · {new Date(m.sentAt).toLocaleString(t.dateLocale)}</div>
                      <div style={{ whiteSpace: 'pre-wrap' }}>{m.body}</div>
                    </div>
                  ))}
                </div>
                <form onSubmit={handleReply} className="formGrid">
                  <TextArea value={reply} onChange={e => setReply(e.target.value)} rows={3} placeholder={t.replyPlaceholder} maxLength={5000} />
                  <div className="formActions">
                    <Button type="submit" disabled={sending || !reply.trim()}>{sending ? t.sending : t.send}</Button>
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
