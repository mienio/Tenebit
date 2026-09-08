import { ArrowRight, Banknote, Check, LineChart, Link2, ShieldCheck, Zap } from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { BrandMark } from '../components/BrandMark';
import { PublicFooter } from '../components/PublicFooter';
import { PartnerLanguageSwitch, usePartnerLocale, type PartnerLocale } from './i18n';
import './partner.css';

const content: Record<PartnerLocale, {
  loginLabel: string;
  ctaLabel: string;
  eyebrow: string;
  heroTitle: string;
  heroLead: string;
  haveAccount: string;
  trust: string[];
  benefitsEyebrow: string;
  benefitsTitle: string;
  benefits: { icon: typeof Banknote; title: string; text: string }[];
  stepsTitle: string;
  steps: { title: string; text: string }[];
  pricingTitle: string;
  pricingLead: string;
}> = {
  en: {
    loginLabel: 'Log in',
    ctaLabel: 'Become a partner',
    eyebrow: 'Tenebit Partner Program',
    heroTitle: 'Recommend Tenebit and we’ll share the profit',
    heroLead: 'Got a website, a channel, customers, or just friends who run companies? Grab your promo code and earn a commission on every person who starts using Tenebit through you. No limits, no exclusivity contracts.',
    haveAccount: 'I already have an account',
    trust: ['10% commission to start', 'Payouts to PayPal or Revolut', 'Buyer data always anonymous'],
    benefitsEyebrow: 'Why join',
    benefitsTitle: 'Earn by recommending a tool you actually believe in',
    benefits: [
      { icon: Banknote, title: '10% commission to start on every sale', text: 'For every person who buys a Tenebit plan with your promo code, you get a commission - renewing every month for as long as the customer stays with us.' },
      { icon: Zap, title: 'Payouts to PayPal or Revolut, no delay', text: 'You receive your earnings directly to your PayPal or Revolut account - your choice. No invoices to fill out, no weeks of waiting.' },
      { icon: LineChart, title: 'A dashboard with live sales tracking', text: 'See the date, commission amount, and code used for every sale. No limit on the number of codes or earnings.' },
      { icon: ShieldCheck, title: 'Buyer data stays private', text: 'The sales list is fully anonymized - you see your results, never the customer’s personal data.' },
    ],
    stepsTitle: 'Three steps stand between you and your first commission',
    steps: [
      { title: 'Create a partner account', text: 'Registration takes a minute - give your email, confirm the code, and you’re done.' },
      { title: 'Share your promo code', text: 'Paste it on your website, on social media, or hand it directly to customers.' },
      { title: 'Collect your commission', text: 'Every paid subscription with your promo code means a commission in your account - visible in the dashboard right away.' },
    ],
    pricingTitle: 'Ready to start earning?',
    pricingLead: 'Registration takes a minute. You get your code right after confirming your email.',
  },
  pl: {
    loginLabel: 'Zaloguj się',
    ctaLabel: 'Zostań partnerem',
    eyebrow: 'Program partnerski Tenebit',
    heroTitle: 'Poleć Tenebit, a my podzielimy się zyskiem',
    heroLead: 'Masz stronę, kanał, klientów albo po prostu znajomych z firmami? Zgarnij swój kod promocyjny i zarabiaj prowizję od każdej osoby, która dzięki Tobie zacznie korzystać z Tenebit. Bez limitu, bez umów na wyłączność.',
    haveAccount: 'Mam już konto',
    trust: ['10% prowizji na start', 'Wypłaty na PayPal lub Revolut', 'Dane kupujących zawsze anonimowe'],
    benefitsEyebrow: 'Dlaczego warto',
    benefitsTitle: 'Zarabiaj, polecając narzędzie, w które sam wierzysz',
    benefits: [
      { icon: Banknote, title: 'Prowizja na start 10% od każdej sprzedaży', text: 'Za każdą osobę, która kupi plan Tenebit z Twoim kodem promocyjnym, dostajesz prowizję - odnawiającą się co miesiąc, dopóki klient zostaje z nami.' },
      { icon: Zap, title: 'Wypłaty na PayPal lub Revolut, bez zwłoki', text: 'Zarobione środki odbierasz bezpośrednio na swoje konto PayPal lub Revolut - Ty wybierasz. Żadnych faktur do wypełniania ani czekania tygodniami.' },
      { icon: LineChart, title: 'Panel z podglądem sprzedaży na żywo', text: 'Widzisz datę, kwotę prowizji i użyty kod przy każdej sprzedaży. Bez limitu liczby kodów ani zarobków.' },
      { icon: ShieldCheck, title: 'Dane kupujących zostają prywatne', text: 'Lista sprzedaży jest w pełni zanonimizowana - Ty widzisz swój wynik, nigdy dane osobowe klienta.' },
    ],
    stepsTitle: 'Trzy kroki dzielą Cię od pierwszej prowizji',
    steps: [
      { title: 'Załóż konto partnera', text: 'Rejestracja zajmuje minutę - podajesz e-mail, potwierdzasz kod i gotowe.' },
      { title: 'Udostępniaj swój kod promocyjny', text: 'Wklej go na swojej stronie, w social mediach albo przekaż bezpośrednio klientom.' },
      { title: 'Odbieraj prowizję', text: 'Każda płatna subskrypcja z Twoim kodem promocyjnym to prowizja na Twoim koncie - widoczna w panelu od razu.' },
    ],
    pricingTitle: 'Gotowy, żeby zacząć zarabiać?',
    pricingLead: 'Rejestracja zajmuje minutę. Kod dostajesz od razu po potwierdzeniu e-maila.',
  },
};

export function PartnerLandingPage() {
  const [scrolled, setScrolled] = useState(false);
  const { path, locale } = usePartnerLocale();
  const t = content[locale];

  useEffect(() => {
    const onScroll = () => setScrolled(window.scrollY > 8);
    onScroll();
    window.addEventListener('scroll', onScroll, { passive: true });
    return () => window.removeEventListener('scroll', onScroll);
  }, []);

  return (
    <div className="landing">
      <header className={`landing__nav${scrolled ? ' landing__nav--scrolled' : ''}`}>
        <Link to="/" className="landing__brand" aria-label="Tenebit">
          <span className="brand__mark"><BrandMark /></span>
          <strong>Tenebit</strong>
        </Link>
        <div className="landing__navActions">
          <PartnerLanguageSwitch className="button button--ghost landing__loginButton" />
          <Link to={path('login')} className="button button--ghost landing__loginButton">{t.loginLabel}</Link>
          <Link to={path('register')} className="button button--primary">{t.ctaLabel}</Link>
        </div>
      </header>

      <main className="landing__main">
        <div className="landing__glowWrap">
          <div className="landing__glow landing__glow--one" aria-hidden="true" />
          <div className="landing__glow landing__glow--two" aria-hidden="true" />

          <section className="landing__hero">
            <p className="eyebrow">{t.eyebrow}</p>
            <h1>{t.heroTitle}</h1>
            <p className="landing__lead">{t.heroLead}</p>
            <div className="landing__heroActions">
              <Link to={path('register')} className="button button--primary">{t.ctaLabel} <ArrowRight size={16} /></Link>
              <Link to={path('login')} className="button button--secondary">{t.haveAccount}</Link>
            </div>
            <div className="landing__trustRow">
              {t.trust.map(item => <span key={item}><Check size={14} /> {item}</span>)}
            </div>
          </section>
        </div>

        <section className="landing__personas" aria-labelledby="partner-benefits-title">
          <div className="landing__sectionIntro">
            <p className="eyebrow">{t.benefitsEyebrow}</p>
            <h2 id="partner-benefits-title">{t.benefitsTitle}</h2>
          </div>
          <div className="landing__personaGrid">
            {t.benefits.map(benefit => (
              <article className="landing__personaCard" key={benefit.title}>
                <div className="landing__personaRole"><benefit.icon size={18} /> Partner</div>
                <h3>{benefit.title}</h3>
                <p>{benefit.text}</p>
              </article>
            ))}
          </div>
        </section>

        <section className="landing__steps">
          <h2>{t.stepsTitle}</h2>
          <div className="landing__stepGrid">
            {t.steps.map((step, index) => (
              <div className="landing__stepCard" key={step.title}>
                <span className="landing__stepNumber">{index + 1}</span>
                <h3>{step.title}</h3>
                <p>{step.text}</p>
              </div>
            ))}
          </div>
        </section>

        <section className="landing__pricing">
          <h2>{t.pricingTitle}</h2>
          <p style={{ maxWidth: 520, margin: '0 auto 20px', color: 'var(--muted)' }}>{t.pricingLead}</p>
          <Link to={path('register')} className="button button--primary">{t.ctaLabel} <Link2 size={16} /></Link>
        </section>
      </main>
      <PublicFooter language={locale} />
    </div>
  );
}
