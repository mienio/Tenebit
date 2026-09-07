import { FormEvent, useState } from 'react';
import { Button } from '../components/Button';
import { Field, TextInput } from '../components/FormFields';
import { PartnerLayout, PartnerPageHeader } from './PartnerLayout';
import { usePartnerAuth } from './PartnerAuthProvider';
import { updateMyProfile } from './partnerApi';

export function PartnerProfilePage() {
  const { affiliate, refreshProfile } = usePartnerAuth();
  const [firstName, setFirstName] = useState(affiliate?.firstName ?? '');
  const [lastName, setLastName] = useState(affiliate?.lastName ?? '');
  const [phoneNumber, setPhoneNumber] = useState(affiliate?.phoneNumber ?? '');
  const [countryCode, setCountryCode] = useState(affiliate?.countryCode ?? '');
  const [companyName, setCompanyName] = useState(affiliate?.companyName ?? '');
  const [taxId, setTaxId] = useState(affiliate?.taxId ?? '');
  const [revolutTag, setRevolutTag] = useState(affiliate?.revolutTag ?? '');
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
        revolutTag: revolutTag.trim() || null,
      });
      await refreshProfile();
      setSuccess('Zapisano zmiany.');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Nie udało się zapisać zmian.');
    } finally {
      setSaving(false);
    }
  }

  if (!affiliate) return null;

  return (
    <PartnerLayout>
      <PartnerPageHeader title="Twój profil" description={affiliate.email} />
      {!affiliate.revolutTag ? (
        <p className="formMessage formMessage--error">
          Uzupełnij revtag Revolut poniżej - bez niego nie możemy zrealizować wypłaty prowizji.
        </p>
      ) : null}
      <div className="card">
        <form className="formGrid" onSubmit={handleSubmit}>
          <Field label="Imię">
            <TextInput value={firstName} onChange={e => setFirstName(e.target.value)} required />
          </Field>
          <Field label="Nazwisko">
            <TextInput value={lastName} onChange={e => setLastName(e.target.value)} required />
          </Field>
          <Field label="Telefon (opcjonalnie)">
            <TextInput value={phoneNumber} onChange={e => setPhoneNumber(e.target.value)} />
          </Field>
          <Field label="Kraj (kod ISO, opcjonalnie)">
            <TextInput value={countryCode} onChange={e => setCountryCode(e.target.value.toUpperCase())} maxLength={2} />
          </Field>
          <Field label="Firma (opcjonalnie)">
            <TextInput value={companyName} onChange={e => setCompanyName(e.target.value)} />
          </Field>
          <Field label="NIP (opcjonalnie)">
            <TextInput value={taxId} onChange={e => setTaxId(e.target.value)} />
          </Field>
          <Field label="Revtag Revolut" info="Format @nazwa - tam trafiają wypłaty prowizji.">
            <TextInput value={revolutTag} onChange={e => setRevolutTag(e.target.value)} placeholder="@twoja-nazwa" />
          </Field>
          {error ? <p className="formMessage formMessage--error">{error}</p> : null}
          {success ? <p className="formMessage formMessage--success">{success}</p> : null}
          <div className="formActions">
            <Button type="submit" disabled={saving}>{saving ? 'Zapisywanie…' : 'Zapisz zmiany'}</Button>
          </div>
        </form>
      </div>
    </PartnerLayout>
  );
}
