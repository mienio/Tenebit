import { ShieldCheck } from 'lucide-react';
import { FormEvent, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { adminLogin } from './adminApi';
import './admin.css';

export function AdminLoginPage() {
  const navigate = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    setError(null);
    setSubmitting(true);
    try {
      await adminLogin(String(form.get('email') ?? ''), String(form.get('password') ?? ''), String(form.get('totpCode') ?? ''));
      navigate('/admin', { replace: true });
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Logowanie nie powiodło się.');
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <main className="adminLoginShell">
      <section className="adminLoginCard">
        <h1><ShieldCheck size={20} /> Panel administracyjny</h1>
        <p>Dostęp wyłącznie dla konta systemowego. Wymagane hasło oraz kod z aplikacji uwierzytelniającej.</p>
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label="E-mail"><TextInput name="email" type="email" required autoFocus autoComplete="off" /></Field>
          <Field label="Hasło"><TextInput name="password" type="password" required autoComplete="off" /></Field>
          <Field label="Kod 2FA" info="6-cyfrowy kod z aplikacji uwierzytelniającej">
            <TextInput name="totpCode" inputMode="numeric" maxLength={6} minLength={6} required autoComplete="one-time-code" />
          </Field>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          <Button disabled={submitting} icon={<ShieldCheck size={16} />}>{submitting ? 'Loguję…' : 'Zaloguj się'}</Button>
        </form>
      </section>
    </main>
  );
}
