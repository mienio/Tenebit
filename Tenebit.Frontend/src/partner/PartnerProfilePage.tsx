import { FormEvent, useState } from 'react';
import { Button } from '../components/Button';
import { Field, SelectInput, TextInput } from '../components/FormFields';
import { PartnerLayout, PartnerPageHeader } from './PartnerLayout';
import { usePartnerAuth } from './PartnerAuthProvider';
import { updateMyProfile } from './partnerApi';
import { usePartnerLocale, type PartnerLocale } from './i18n';

const content: Record<PartnerLocale, {
  title: string; missingRevtag: string; saveError: string; saveSuccess: string;
  firstName: string; lastName: string; phone: string; country: string; company: string; taxId: string;
  payoutMethod: string; revtagInfo: string; revtagPlaceholderRevolut: string; revtagPlaceholderPayPal: string;
  save: string; saving: string;
}> = {
  en: {
    title: 'Your profile',
    missingRevtag: 'Add your PayPal/Revolut account below - without it we cannot pay out your commission.',
    saveError: 'Could not save changes.', saveSuccess: 'Changes saved.',
    firstName: 'First name', lastName: 'Last name', phone: 'Phone (optional)',
    country: 'Country (ISO code, optional)', company: 'Company (optional)', taxId: 'Tax ID (optional)',
    payoutMethod: 'PayPal / Revolut account',
    revtagInfo: 'Revolut: format @name. PayPal: your account e-mail. Commission payouts go there.',
    revtagPlaceholderRevolut: '@your-name', revtagPlaceholderPayPal: 'you@example.com',
    save: 'Save changes', saving: 'Saving…',
  },
  pl: {
    title: 'Twój profil',
    missingRevtag: 'Uzupełnij konto PayPal/Revolut poniżej - bez niego nie możemy zrealizować wypłaty prowizji.',
    saveError: 'Nie udało się zapisać zmian.', saveSuccess: 'Zapisano zmiany.',
    firstName: 'Imię', lastName: 'Nazwisko', phone: 'Telefon (opcjonalnie)',
    country: 'Kraj (kod ISO, opcjonalnie)', company: 'Firma (opcjonalnie)', taxId: 'NIP (opcjonalnie)',
    payoutMethod: 'Konto PayPal / Revolut',
    revtagInfo: 'Revolut: format @nazwa. PayPal: e-mail Twojego konta. Tam trafiają wypłaty prowizji.',
    revtagPlaceholderRevolut: '@twoja-nazwa', revtagPlaceholderPayPal: 'ty@example.com',
    save: 'Zapisz zmiany', saving: 'Zapisywanie…',
  },
};

export function PartnerProfilePage() {
  const { locale } = usePartnerLocale();
  const t = content[locale];
  const { affiliate, refreshProfile } = usePartnerAuth();
  const [firstName, setFirstName] = useState(affiliate?.firstName ?? '');
  const [lastName, setLastName] = useState(affiliate?.lastName ?? '');
  const [phoneNumber, setPhoneNumber] = useState(affiliate?.phoneNumber ?? '');
  const [countryCode, setCountryCode] = useState(affiliate?.countryCode ?? '');
  const [companyName, setCompanyName] = useState(affiliate?.companyName ?? '');
  const [taxId, setTaxId] = useState(affiliate?.taxId ?? '');
  const [payoutMethod, setPayoutMethod] = useState<'Revolut' | 'PayPal'>(affiliate?.payoutMethod ?? 'Revolut');
  const [payoutAccountTag, setPayoutAccountTag] = useState(affiliate?.payoutAccountTag ?? '');
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();
    setError(null);
    setSuccess(null);
    setSaving(true);
    try {
      await updateMyProfile({
        firstName, lastName,
        phoneNumber: phoneNumber.trim() || null,
        countryCode: countryCode.trim() || null,
        companyName: companyName.trim() || null,
        taxId: taxId.trim() || null,
        payoutMethod,
        payoutAccountTag: payoutAccountTag.trim() || null,
      });
      await refreshProfile();
      setSuccess(t.saveSuccess);
    } catch (err) {
      setError(err instanceof Error ? err.message : t.saveError);
    } finally {
      setSaving(false);
    }
  }

  if (!affiliate) return null;

  return (
    <PartnerLayout>
      <PartnerPageHeader title={t.title} description={affiliate.email} />
      {!affiliate.payoutAccountTag ? (
        <p className="formMessage formMessage--error">{t.missingRevtag}</p>
      ) : null}
      <div className="card">
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label={t.firstName}>
            <TextInput value={firstName} onChange={e => setFirstName(e.target.value)} required />
          </Field>
          <Field label={t.lastName}>
            <TextInput value={lastName} onChange={e => setLastName(e.target.value)} required />
          </Field>
          <Field label={t.phone}>
            <TextInput value={phoneNumber} onChange={e => setPhoneNumber(e.target.value)} />
          </Field>
          <Field label={t.country}>
            <TextInput value={countryCode} onChange={e => setCountryCode(e.target.value.toUpperCase())} maxLength={2} />
          </Field>
          <Field label={t.company}>
            <TextInput value={companyName} onChange={e => setCompanyName(e.target.value)} />
          </Field>
          <Field label={t.taxId}>
            <TextInput value={taxId} onChange={e => setTaxId(e.target.value)} />
          </Field>
          <Field label={t.payoutMethod} info={t.revtagInfo}>
            <SelectInput value={payoutMethod} onChange={e => setPayoutMethod(e.target.value as 'Revolut' | 'PayPal')}>
              <option value="Revolut">Revolut</option>
              <option value="PayPal">PayPal</option>
            </SelectInput>
            <TextInput
              value={payoutAccountTag}
              onChange={e => setPayoutAccountTag(e.target.value)}
              placeholder={payoutMethod === 'PayPal' ? t.revtagPlaceholderPayPal : t.revtagPlaceholderRevolut}
              style={{ marginTop: 8 }}
            />
          </Field>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          {success ? <p className="formMessage formMessage--success">{success}</p> : null}
          <div className="formActions">
            <Button type="submit" disabled={saving}>{saving ? t.saving : t.save}</Button>
          </div>
        </form>
      </div>
    </PartnerLayout>
  );
}
