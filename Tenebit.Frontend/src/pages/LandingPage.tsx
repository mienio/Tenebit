import {
  ArrowRight,
  BarChart3,
  Boxes,
  Check,
  ClipboardCheck,
  Headphones,
  KeyRound,
  Laptop,
  List,
  MapPin,
  Monitor,
  PackageCheck,
  QrCode,
  Smartphone,
  Users,
  Wrench
} from 'lucide-react';
import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Avatar } from '../components/Avatar';
import { BrandMark } from '../components/BrandMark';
import { LocationTree } from '../components/LocationTree';
import { PricingCards } from '../components/PricingCards';
import { PublicFooter } from '../components/PublicFooter';
import { MaintenanceDueRow } from '../components/MaintenanceDue';
import { StatusBadge } from '../components/StatusBadge';
import { useI18n } from '../i18n/I18nProvider';
import type { Language } from '../i18n/translations';
import { LanguageSwitcher } from '../i18n/LanguageSwitcher';
import type { LocationNode } from '../types/domain';

// previewPeopleByLanguage keeps the same gender per index across all 4 locales (index 0/3 female,
// 1/2/4 male), so one photo per index works for every language.
const previewAvatarUrls = [
  'https://randomuser.me/api/portraits/women/65.jpg',
  'https://randomuser.me/api/portraits/men/32.jpg',
  'https://randomuser.me/api/portraits/men/86.jpg',
  'https://randomuser.me/api/portraits/women/44.jpg',
  'https://randomuser.me/api/portraits/men/76.jpg'
];

const previewAssetRows = [
  { icon: Laptop, name: 'MacBook Pro 14"', tag: 'AST-0142', status: 'Assigned', personIndex: 0, locationId: 'room101', value: 9800 },
  { icon: Laptop, name: 'Dell Latitude 5420', tag: 'AST-0156', status: 'Assigned', personIndex: 1, locationId: 'room101', value: 6200 },
  { icon: Smartphone, name: 'iPhone 15', tag: 'AST-0198', status: 'Assigned', personIndex: 2, locationId: 'room102', value: 4200 },
  { icon: Monitor, name: 'Dell UltraSharp 27"', tag: 'AST-0071', status: 'InStock', personIndex: null, locationId: 'room201', value: 1650 },
  { icon: Headphones, name: 'Sony WH-1000XM5', tag: 'AST-0233', status: 'InService', personIndex: null, locationId: 'room202', value: 1400 }
] as const;

// Single source of truth for where each preview person sits — feeds both the people table's
// location column and the location tree's counts, so they can never drift out of sync.
const previewPersonLocationIds = ['room101', 'room101', 'room102', 'room201', 'room202'] as const;

const previewRoomAssetCounts: Record<'room101' | 'room102' | 'room201' | 'room202', number> = { room101: 0, room102: 0, room201: 0, room202: 0 };
for (const row of previewAssetRows) previewRoomAssetCounts[row.locationId]++;

const previewRoomPersonCounts: Record<'room101' | 'room102' | 'room201' | 'room202', number> = { room101: 0, room102: 0, room201: 0, room202: 0 };
for (const locationId of previewPersonLocationIds) previewRoomPersonCounts[locationId]++;

const previewFloor1AssetCount = previewRoomAssetCounts.room101 + previewRoomAssetCounts.room102;
const previewFloor2AssetCount = previewRoomAssetCounts.room201 + previewRoomAssetCounts.room202;
const previewFloor1PersonCount = previewRoomPersonCounts.room101 + previewRoomPersonCounts.room102;
const previewFloor2PersonCount = previewRoomPersonCounts.room201 + previewRoomPersonCounts.room202;
const previewBuildingAssetCount = previewFloor1AssetCount + previewFloor2AssetCount;
const previewBuildingPersonCount = previewFloor1PersonCount + previewFloor2PersonCount;

// Windows is bound to a single device via one product key, not a shared seat pool like the
// subscriptions below - shown as 1/1 with its key instead of a fake team-wide seat count.
// Slack/Figma are seat-based SaaS with no license key at all, which is realistic too.
const previewLicenseRows = [
  { name: 'Windows 11 Pro', vendor: 'Microsoft', seatsUsed: 1, seatsTotal: 1, status: 'Active', key: 'KSG19-••••-••••-••••-48FTW' },
  { name: 'Microsoft 365 Business', vendor: 'Microsoft', seatsUsed: 50, seatsTotal: 50, status: 'Active', key: '4835-••••-••••-••••-0407' },
  { name: 'Adobe Creative Cloud', vendor: 'Adobe', seatsUsed: 6, seatsTotal: 15, status: 'Active', key: 'T9V3K-••••-••••-2LWPZ' },
  { name: 'JetBrains All Products', vendor: 'JetBrains', seatsUsed: 18, seatsTotal: 20, status: 'Expired', key: '4KXQL-••••-••••-R2MNC' },
  { name: 'Slack Business+', vendor: 'Slack', seatsUsed: 40, seatsTotal: 50, status: 'Active', key: null },
  { name: 'Figma Organization', vendor: 'Figma', seatsUsed: 12, seatsTotal: 15, status: 'Active', key: null }
];

// One office per language/market, split Building -> 2 Floors -> 2 Rooms each, to demo the location
// hierarchy without implying the company has multiple real-world sites.
const previewOfficeByLanguage: Record<Language, { building: string; floor1: string; floor2: string; room101: string; room102: string; room201: string; room202: string }> = {
  pl: { building: 'Warszawa, ul. Prosta 20', floor1: 'Piętro 1', floor2: 'Piętro 2', room101: 'Pokój 101', room102: 'Pokój 102', room201: 'Pokój 201', room202: 'Pokój 202' },
  en: { building: 'London, 24 Borough High St', floor1: 'Floor 1', floor2: 'Floor 2', room101: 'Room 101', room102: 'Room 102', room201: 'Room 201', room202: 'Room 202' },
  es: { building: 'Madrid, Calle de Alcalá 45', floor1: 'Planta 1', floor2: 'Planta 2', room101: 'Sala 101', room102: 'Sala 102', room201: 'Sala 201', room202: 'Sala 202' },
  de: { building: 'Berlin, Torstraße 15', floor1: 'Etage 1', floor2: 'Etage 2', room101: 'Raum 101', room102: 'Raum 102', room201: 'Raum 201', room202: 'Raum 202' }
};

const previewPeopleByLanguage: Record<Language, { name: string; jobTitle: string; team: string }[]> = {
  pl: [
    { name: 'Anna Kowalska', jobTitle: 'Office Manager', team: 'Administracja' },
    { name: 'Piotr Kaczmarek', jobTitle: 'IT Support Specialist', team: 'IT' },
    { name: 'Marek Wiśniewski', jobTitle: 'DevOps Engineer', team: 'IT' },
    { name: 'Julia Nowak', jobTitle: 'Account Executive', team: 'Sprzedaż' },
    { name: 'Tomasz Zieliński', jobTitle: 'HR Specialist', team: 'Kadry' }
  ],
  en: [
    { name: 'Olivia Bennett', jobTitle: 'Office Manager', team: 'Admin' },
    { name: 'Noah Clarke', jobTitle: 'IT Support Specialist', team: 'IT' },
    { name: 'James Carter', jobTitle: 'DevOps Engineer', team: 'IT' },
    { name: 'Sophie Turner', jobTitle: 'Account Executive', team: 'Sales' },
    { name: 'Daniel Wright', jobTitle: 'HR Specialist', team: 'People' }
  ],
  es: [
    { name: 'Lucía Fernández', jobTitle: 'Office Manager', team: 'Administración' },
    { name: 'Diego Torres', jobTitle: 'IT Support Specialist', team: 'IT' },
    { name: 'Javier Martín', jobTitle: 'DevOps Engineer', team: 'IT' },
    { name: 'Marta Sánchez', jobTitle: 'Account Executive', team: 'Ventas' },
    { name: 'Pablo Ruiz', jobTitle: 'HR Specialist', team: 'RRHH' }
  ],
  de: [
    { name: 'Hannah Fischer', jobTitle: 'Office Manager', team: 'Verwaltung' },
    { name: 'Jonas Becker', jobTitle: 'IT Support Specialist', team: 'IT' },
    { name: 'Lukas Weber', jobTitle: 'DevOps Engineer', team: 'IT' },
    { name: 'Laura Schmidt', jobTitle: 'Account Executive', team: 'Vertrieb' },
    { name: 'Felix Wagner', jobTitle: 'HR Specialist', team: 'Personal' }
  ]
};

// personIndex 1 is the IT Support Specialist in previewPeopleByLanguage — shown here as the
// person who just went through onboarding, to demo that procedures get assigned to someone.
const previewProcedureRowsByLanguage: Record<Language, { title: string; version: string; status: string; personIndex: number | null; scope: string | null }[]> = {
  pl: [
    { title: 'Onboarding — sprzęt i BHP', version: 'v3', status: 'Published', personIndex: 1, scope: null },
    { title: 'Polityka bezpieczeństwa danych', version: 'v2', status: 'Published', personIndex: null, scope: 'Wszyscy pracownicy' },
    { title: 'Regulamin pracy zdalnej', version: 'v1', status: 'Published', personIndex: null, scope: 'Wszyscy pracownicy' },
    { title: 'Zgłaszanie awarii sprzętu', version: 'v2', status: 'Published', personIndex: null, scope: 'Dział IT' },
    { title: 'BHP — stanowisko z monitorem ekranowym', version: 'v4', status: 'Published', personIndex: null, scope: 'Wszyscy pracownicy' },
    { title: 'Offboarding — zwrot sprzętu', version: 'v1', status: 'Draft', personIndex: null, scope: null }
  ],
  en: [
    { title: 'Onboarding — equipment & safety', version: 'v3', status: 'Published', personIndex: 1, scope: null },
    { title: 'Data security policy', version: 'v2', status: 'Published', personIndex: null, scope: 'All employees' },
    { title: 'Remote work policy', version: 'v1', status: 'Published', personIndex: null, scope: 'All employees' },
    { title: 'Equipment fault reporting', version: 'v2', status: 'Published', personIndex: null, scope: 'IT department' },
    { title: 'Display screen workstation safety', version: 'v4', status: 'Published', personIndex: null, scope: 'All employees' },
    { title: 'Offboarding — equipment return', version: 'v1', status: 'Draft', personIndex: null, scope: null }
  ],
  es: [
    { title: 'Incorporación — equipo y seguridad', version: 'v3', status: 'Published', personIndex: 1, scope: null },
    { title: 'Política de seguridad de datos', version: 'v2', status: 'Published', personIndex: null, scope: 'Todos los empleados' },
    { title: 'Política de trabajo remoto', version: 'v1', status: 'Published', personIndex: null, scope: 'Todos los empleados' },
    { title: 'Reporte de averías de equipos', version: 'v2', status: 'Published', personIndex: null, scope: 'Departamento de IT' },
    { title: 'Seguridad — puesto con pantalla', version: 'v4', status: 'Published', personIndex: null, scope: 'Todos los empleados' },
    { title: 'Baja — devolución de equipo', version: 'v1', status: 'Draft', personIndex: null, scope: null }
  ],
  de: [
    { title: 'Onboarding — Ausstattung & Sicherheit', version: 'v3', status: 'Published', personIndex: 1, scope: null },
    { title: 'Datensicherheitsrichtlinie', version: 'v2', status: 'Published', personIndex: null, scope: 'Alle Mitarbeitenden' },
    { title: 'Richtlinie für Remote-Arbeit', version: 'v1', status: 'Published', personIndex: null, scope: 'Alle Mitarbeitenden' },
    { title: 'Meldung von Gerätestörungen', version: 'v2', status: 'Published', personIndex: null, scope: 'IT-Abteilung' },
    { title: 'Arbeitsschutz — Bildschirmarbeitsplatz', version: 'v4', status: 'Published', personIndex: null, scope: 'Alle Mitarbeitenden' },
    { title: 'Offboarding — Rückgabe der Ausstattung', version: 'v1', status: 'Draft', personIndex: null, scope: null }
  ]
};

// Mocked schedules for the preview. Chosen to show all three urgency states at once - overdue, due
// soon and comfortably ahead - because that contrast is what makes the panel legible at a glance.
const previewMaintenanceRows = [
  { nameKey: 'landing.preview.maintenance.extinguisher', assetKey: 'landing.preview.maintenance.extinguisherAsset', daysRemaining: -4, cycleProgress: 100 },
  { nameKey: 'landing.preview.maintenance.ladder', assetKey: 'landing.preview.maintenance.ladderAsset', daysRemaining: 11, cycleProgress: 94 },
  { nameKey: 'landing.preview.maintenance.electrical', assetKey: 'landing.preview.maintenance.electricalAsset', daysRemaining: 63, cycleProgress: 65 },
  { nameKey: 'landing.preview.maintenance.hvac', assetKey: 'landing.preview.maintenance.hvacAsset', daysRemaining: 172, cycleProgress: 42 }
];

const previewTabs = [
  { key: 'assets', icon: Boxes },
  { key: 'licenses', icon: KeyRound },
  { key: 'people', icon: Users },
  { key: 'locations', icon: MapPin },
  { key: 'procedures', icon: ClipboardCheck },
  { key: 'maintenance', icon: Wrench }
] as const;

type PreviewTab = typeof previewTabs[number]['key'];

const features = [
  { icon: Boxes, key: 'assets' },
  { icon: Users, key: 'people' },
  { icon: PackageCheck, key: 'assignments' },
  { icon: ClipboardCheck, key: 'procedures' },
  { icon: QrCode, key: 'qr' },
  { icon: BarChart3, key: 'reports' }
] as const;

const steps = ['step1', 'step2', 'step3'];

export function LandingPage() {
  const { t, language } = useI18n();
  const [scrolled, setScrolled] = useState(false);
  const [previewTab, setPreviewTab] = useState<PreviewTab>('assets');
  const [assetView, setAssetView] = useState<'list' | 'byLocation'>('list');
  const [selectedLocationId, setSelectedLocationId] = useState<string | null>('room101');
  const office = previewOfficeByLanguage[language];
  const officeRoomById: Record<'room101' | 'room102' | 'room201' | 'room202', string> = {
    room101: `${office.floor1} · ${office.room101}`,
    room102: `${office.floor1} · ${office.room102}`,
    room201: `${office.floor2} · ${office.room201}`,
    room202: `${office.floor2} · ${office.room202}`
  };
  const previewPeopleRows = previewPeopleByLanguage[language].map((person, index) => ({
    ...person,
    location: officeRoomById[previewPersonLocationIds[index]]
  }));
  const previewProcedureRows = previewProcedureRowsByLanguage[language];
  const previewLocationNodes: LocationNode[] = [
    { id: 'building', name: office.building, type: 'Building', parentId: null, fullPath: office.building, assetCount: previewBuildingAssetCount, personCount: previewBuildingPersonCount, isActive: true },
    { id: 'floor1', name: office.floor1, type: 'Floor', parentId: 'building', fullPath: `${office.building} · ${office.floor1}`, assetCount: previewFloor1AssetCount, personCount: previewFloor1PersonCount, isActive: true },
    { id: 'room101', name: office.room101, type: 'Room', parentId: 'floor1', fullPath: `${office.building} · ${office.floor1} · ${office.room101}`, assetCount: previewRoomAssetCounts.room101, personCount: previewRoomPersonCounts.room101, isActive: true },
    { id: 'room102', name: office.room102, type: 'Room', parentId: 'floor1', fullPath: `${office.building} · ${office.floor1} · ${office.room102}`, assetCount: previewRoomAssetCounts.room102, personCount: previewRoomPersonCounts.room102, isActive: true },
    { id: 'floor2', name: office.floor2, type: 'Floor', parentId: 'building', fullPath: `${office.building} · ${office.floor2}`, assetCount: previewFloor2AssetCount, personCount: previewFloor2PersonCount, isActive: true },
    { id: 'room201', name: office.room201, type: 'Room', parentId: 'floor2', fullPath: `${office.building} · ${office.floor2} · ${office.room201}`, assetCount: previewRoomAssetCounts.room201, personCount: previewRoomPersonCounts.room201, isActive: true },
    { id: 'room202', name: office.room202, type: 'Room', parentId: 'floor2', fullPath: `${office.building} · ${office.floor2} · ${office.room202}`, assetCount: previewRoomAssetCounts.room202, personCount: previewRoomPersonCounts.room202, isActive: true }
  ];
  // Identical <tr> markup used by both the flat "list" view and the nested "by location" view,
  // so an asset row looks the same everywhere instead of drifting into a custom layout.
  const renderAssetRow = (row: typeof previewAssetRows[number]) => {
    const personIndex = row.personIndex;
    const person = personIndex != null ? previewPeopleRows[personIndex] : null;
    return (
      <tr key={row.tag}>
        <td><div className="table-icon"><row.icon size={16} /></div></td>
        <td><strong>{row.name}</strong></td>
        <td>{row.tag}</td>
        <td><StatusBadge status={row.status} /></td>
        <td>{person && personIndex != null ? <span className="personChip"><Avatar name={person.name} photoUrl={previewAvatarUrls[personIndex]} size={22} /><span className="personChip__sep">•</span>{person.name}</span> : t('common.unassigned')}</td>
        <td style={{ textAlign: 'right' }}>{formatPreviewValue(row.value)}</td>
      </tr>
    );
  };
  const renderAssetLocationNode = (node: LocationNode): JSX.Element => {
    const children = previewLocationNodes.filter(n => n.parentId === node.id);
    const assetsHere = previewAssetRows.filter(row => row.locationId === node.id);
    return (
      <div className="locationGroup" key={node.id}>
        <div className="locationRow">
          <div className="locationRow__main">
            <MapPin size={14} />
            <span>
              <strong>{node.name}</strong>
              <small>{t('locations.assetsCount', { count: node.assetCount })} · {t('locations.peopleCount', { count: node.personCount })}</small>
            </span>
          </div>
        </div>
        {(children.length > 0 || assetsHere.length > 0) && (
          <div className="locationGroup__children">
            {children.map(child => renderAssetLocationNode(child))}
            {/* Makieta produktu, nie dane - bez naglowkow kolumn, wiec czytnik ekranu ma ja
                traktowac jak uklad, a nie jak tabele do nawigowania. */}
            {assetsHere.length > 0 && (
              <table className="dense-table" role="presentation">
                <tbody>{assetsHere.map(renderAssetRow)}</tbody>
              </table>
            )}
          </div>
        )}
      </div>
    );
  };
  const currencyByLanguage: Record<typeof language, { locale: string; currency: string }> = {
    pl: { locale: 'pl-PL', currency: 'PLN' },
    en: { locale: 'en-US', currency: 'USD' },
    es: { locale: 'es-ES', currency: 'EUR' },
    de: { locale: 'de-DE', currency: 'EUR' }
  };
  const formatPreviewValue = (value: number) => {
    const { locale, currency } = currencyByLanguage[language];
    return new Intl.NumberFormat(locale, { style: 'currency', currency, maximumFractionDigits: 0 }).format(value);
  };

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
        <nav className="landing__navLinks">
          <a href="#funkcje">{t('landing.navFeatures')}</a>
          <a href="#cennik">{t('landing.navPricing')}</a>
        </nav>
        <div className="landing__navActions">
          <LanguageSwitcher />
          <Link to="/login" className="button button--ghost landing__loginButton">{t('landing.navLoginBtn')}</Link>
          <Link to="/register" className="button button--primary">{t('landing.navRegisterBtn')}</Link>
        </div>
      </header>
      {/* `display: contents` - landmark dla czytnikow ekranu bez zmiany siatki `.landing`. */}
      <main className="landing__main">

      <div className="landing__glowWrap">
        <div className="landing__glow landing__glow--one" aria-hidden="true" />
        <div className="landing__glow landing__glow--two" aria-hidden="true" />

        <section className="landing__hero">
          <p className="eyebrow">{t('landing.eyebrow')}</p>
          <h1>{t('landing.headline')}</h1>
          <p className="landing__lead">{t('landing.lead')}</p>
          <div className="landing__heroActions">
            <Link to="/register" className="button button--primary">{t('landing.ctaStart')} <ArrowRight size={16} /></Link>
            <Link to="/login" className="button button--secondary">{t('landing.ctaLogin')}</Link>
          </div>
          <div className="landing__trustRow">
            <span><Check size={14} /> {t('landing.trust1')}</span>
            <span><Check size={14} /> {t('landing.trust2')}</span>
            <span><Check size={14} /> {t('landing.trust3')}</span>
          </div>
        </section>
      </div>

      <section className="landing__preview" aria-label={t('landing.previewAria')}>
        <div className="landing__previewFrame">
          <div className="landing__previewChrome"><span /><span /><span /></div>
          <div className="landing__previewTabs">
            {previewTabs.map(tab => (
              <button
                key={tab.key}
                type="button"
                className={`landing__previewTab${previewTab === tab.key ? ' landing__previewTab--active' : ''}`}
                onClick={() => setPreviewTab(tab.key)}
              >
                <tab.icon size={14} /> {t(`landing.previewTab.${tab.key}`)}
              </button>
            ))}
          </div>
          {previewTab === 'assets' && (
            <div className="landing__previewSubTabs">
              <button
                type="button"
                className={`landing__previewTab${assetView === 'list' ? ' landing__previewTab--active' : ''}`}
                onClick={() => setAssetView('list')}
              >
                <List size={14} /> {t('landing.preview.viewList')}
              </button>
              <button
                type="button"
                className={`landing__previewTab${assetView === 'byLocation' ? ' landing__previewTab--active' : ''}`}
                onClick={() => setAssetView('byLocation')}
              >
                <MapPin size={14} /> {t('landing.preview.viewByLocation')}
              </button>
            </div>
          )}
          <div className="tableWrap">
            {previewTab === 'assets' && assetView === 'list' && (
              <table className="dense-table">
                <thead>
                  <tr><th></th><th>{t('assets.nameLabel')}</th><th>{t('assets.colTag')}</th><th>{t('assets.statusLabel')}</th><th>{t('assets.colPerson')}</th><th style={{ textAlign: 'right' }}>{t('assets.colValue')}</th></tr>
                </thead>
                <tbody>{previewAssetRows.map(renderAssetRow)}</tbody>
              </table>
            )}
            {previewTab === 'assets' && assetView === 'byLocation' && (
              <div className="locationGroups">
                {previewLocationNodes.filter(node => !node.parentId).map(root => renderAssetLocationNode(root))}
              </div>
            )}
            {previewTab === 'licenses' && (
              <table className="dense-table">
                <thead>
                  <tr><th></th><th>{t('licenses.colName')}</th><th>{t('licenses.colVendor')}</th><th>{t('licenses.colSeats')}</th><th>{t('licenses.colKey')}</th><th>{t('assets.statusLabel')}</th></tr>
                </thead>
                <tbody>
                  {previewLicenseRows.map(row => (
                    <tr key={row.name}>
                      <td><div className="table-icon"><KeyRound size={16} /></div></td>
                      <td><strong>{row.name}</strong></td>
                      <td>{row.vendor}</td>
                      <td>{row.seatsUsed}/{row.seatsTotal}</td>
                      <td>{row.key ? <code>{row.key}</code> : '-'}</td>
                      <td><StatusBadge status={row.status} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            {previewTab === 'people' && (
              <table className="dense-table">
                <thead>
                  <tr><th></th><th>{t('people.colFullName')}</th><th>{t('people.colJobTitle')}</th><th>{t('people.colTeam')}</th><th>{t('landing.preview.colLocation')}</th></tr>
                </thead>
                <tbody>
                  {previewPeopleRows.map((row, index) => (
                    <tr key={row.name}>
                      <td className="cell-icon"><Avatar name={row.name} photoUrl={previewAvatarUrls[index]} size={28} /></td>
                      <td><strong>{row.name}</strong></td>
                      <td>{row.jobTitle}</td>
                      <td>{row.team}</td>
                      <td>{row.location}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
            {previewTab === 'locations' && (
              <LocationTree
                locations={previewLocationNodes}
                selectedId={selectedLocationId}
                onSelect={setSelectedLocationId}
              />
            )}
            {previewTab === 'maintenance' && (
              <div className="dueList landing__previewDue">
                {previewMaintenanceRows.map(row => (
                  <MaintenanceDueRow
                    key={row.nameKey}
                    item={{
                      id: row.nameKey,
                      assetId: row.nameKey,
                      assetName: t(row.assetKey),
                      assetTag: null,
                      name: t(row.nameKey),
                      intervalMonths: 12,
                      nextDueOn: '',
                      lastPerformedOn: null,
                      lastPerformedBy: null,
                      isActive: true,
                      daysRemaining: row.daysRemaining,
                      cycleProgress: row.cycleProgress
                    }}
                  />
                ))}
              </div>
            )}

            {previewTab === 'procedures' && (
              <table className="dense-table">
                <thead>
                  <tr><th></th><th>{t('procedures.titleLabel')}</th><th>{t('procedures.versionLabel')}</th><th>{t('assets.statusLabel')}</th><th>{t('procedures.scopeLabel')}</th></tr>
                </thead>
                <tbody>
                  {previewProcedureRows.map(row => {
                    const person = row.personIndex != null ? previewPeopleRows[row.personIndex] : null;
                    return (
                      <tr key={row.title}>
                        <td><div className="table-icon"><ClipboardCheck size={16} /></div></td>
                        <td><strong>{row.title}</strong></td>
                        <td>{row.version}</td>
                        <td><StatusBadge status={row.status} /></td>
                        <td>{person && row.personIndex != null ? <span className="personChip"><Avatar name={person.name} photoUrl={previewAvatarUrls[row.personIndex]} size={22} /><span className="personChip__sep">•</span>{person.name}</span> : (row.scope ?? t('procedures.noScope'))}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        </div>
      </section>

      <section className="landing__features" id="funkcje">
        <div className="landing__sectionIntro">
          <p className="eyebrow">{t('landing.featuresEyebrow')}</p>
          <h2>{t('landing.featuresHeadline')}</h2>
          <p>{t('landing.featuresLead')}</p>
        </div>

        <div className="landing__featureGrid">
          {features.map(feature => (
            <article className="landing__featureCard" key={feature.key}>
              <h3>{t(`landing.feature.${feature.key}.title`)}</h3>
              <p>{t(`landing.feature.${feature.key}.text`)}</p>
              <blockquote>{t(`landing.feature.${feature.key}.example`)}</blockquote>
            </article>
          ))}
        </div>
      </section>

      <section className="landing__steps">
        <h2>{t('landing.stepsHeadline')}</h2>
        <div className="landing__stepGrid">
          {steps.map((key, index) => (
            <div className="landing__stepCard" key={key}>
              <span className="landing__stepNumber">{index + 1}</span>
              <h3>{t(`landing.${key}.title`)}</h3>
              <p>{t(`landing.${key}.text`)}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="landing__pricing" id="cennik">
        <h2>{t('landing.pricingHeadline')}</h2>
        <PricingCards renderCta={() => null} />
        <Link to="/register" className="button button--primary">{t('landing.ctaStart')} <ArrowRight size={16} /></Link>
      </section>

      </main>
      <PublicFooter />
    </div>
  );
}
