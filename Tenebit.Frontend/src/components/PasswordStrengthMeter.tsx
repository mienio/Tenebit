import { estimatePasswordStrength } from '../utils/passwordStrength';
import { useI18n } from '../i18n/I18nProvider';

export function PasswordStrengthMeter({ password }: { password: string }) {
  const { t } = useI18n();
  if (!password) return null;

  const strength = estimatePasswordStrength(password);
  return (
    <div className={`passwordStrength passwordStrength--${strength}`}>
      <div className="passwordStrength__bar"><span /></div>
      <span className="passwordStrength__label">{t(`auth.passwordStrength.${strength}`)}</span>
    </div>
  );
}
