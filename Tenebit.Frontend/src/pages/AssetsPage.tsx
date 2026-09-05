import { Building2, CircleDot, Download, FileSpreadsheet, Layers, List, Pencil, Plus, RefreshCw, Tag, Upload, Users } from 'lucide-react';
import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { ConfirmDialog } from '../components/ConfirmDialog';
import { Field, SelectInput, TextArea, TextInput } from '../components/FormFields';
import { GroupedAssetBrowser, type AssetGroup } from '../components/GroupedAssetBrowser';
import { IconPicker } from '../components/IconPicker';
import { ImportModal } from '../components/ImportModal';
import { LocationInventoryModal } from '../components/LocationInventoryModal';
import { LocationAssetBrowser } from '../components/LocationAssetBrowser';
import { Modal } from '../components/Modal';
import { PageHeader } from '../components/PageHeader';
import { PersonPreviewModal } from '../components/PersonPreviewModal';
import { ErrorState, LoadingState } from '../components/StateViews';
import { useAsyncData } from '../hooks/useAsyncData';
import type { Asset, AssetCategoryType, AssetStatus, CreateAssetRequest, LocationType, ServiceTicket } from '../types/domain';
import { csvCell, toNullable } from '../utils/format';
import { assetStatusValues, categoryTypeValues, locationTypeValues } from '../utils/labels';
import { useI18n } from '../i18n/I18nProvider';
import { useCelebration } from '../celebration/CelebrationProvider';
import { useAuth } from '../auth/AuthProvider';
import { useSearchParams } from 'react-router-dom';
import { AssetDetailPanel } from './assets/AssetDetailPanel';
import { BatchAddModal } from './assets/BatchAddModal';
import { LabelSheetModal, type LabelSize } from './assets/LabelSheetModal';
import { AssetsList } from './assets/AssetsList';
import { AssetsToolbar } from './assets/AssetsToolbar';
import { ASSET_COLUMNS, readAssetColumns } from './assets/useAssetColumns';
import { useAssetFilters } from './assets/useAssetFilters';
import { useAssetImport } from './assets/useAssetImport';
import { useAssetSelection } from './assets/useAssetSelection';

const pageSize = 25;
type ViewMode = 'list' | 'location' | 'person' | 'status' | 'category';
const assetsViewStorageKey = (email: string) => `tenebit_assets_view_${email}`;


function parseMoney(value: FormDataEntryValue | null) {
  const raw = String(value ?? '').replace(',', '.').trim();
  if (!raw) return null;
  const parsed = Number(raw);
  return Number.isFinite(parsed) ? parsed : null;
}

function saveBlob(blob: Blob, fileName: string) {
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(url);
}

export function AssetsPage() {
  const { t, tPlural } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const { celebrate } = useCelebration();
  const { userEmail } = useAuth();
  const [viewMode, setViewMode] = useState<ViewMode>(() => {
    const stored = window.localStorage.getItem(assetsViewStorageKey(userEmail));
    return stored === 'location' || stored === 'person' || stored === 'status' || stored === 'category' ? stored : 'list';
  });
  useEffect(() => {
    window.localStorage.setItem(assetsViewStorageKey(userEmail), viewMode);
  }, [viewMode, userEmail]);
  const statusSettings = useAsyncData(api.assetStatuses, []);
  const statusSettingByKey = useMemo(() => {
    const map = new Map<string, { label: string; color: string; backgroundColor: string }>();
    for (const item of statusSettings.data ?? []) map.set(item.statusKey, { label: item.label, color: item.color, backgroundColor: item.backgroundColor });
    return map;
  }, [statusSettings.data]);
  const statuses: { value: AssetStatus | ''; label: string }[] = useMemo(() => [
    { value: '', label: t('assets.allStatuses') },
    ...(statusSettings.data?.length
      ? [...statusSettings.data].filter(item => item.isEnabled).sort((a, b) => a.sortOrder - b.sortOrder).map(item => ({ value: item.statusKey, label: item.label }))
      : assetStatusValues.map(value => ({ value, label: t(`status.${value}`) })))
  ], [statusSettings.data, t]);
  const {
    search, setSearch, status, setStatus, location, setLocation, team, setTeam, owner, setOwner,
    warranty, setWarranty, page, setPage, sort, toggleSort, debouncedSearch, clearFilters, hasFilters, openAssetId
  } = useAssetFilters();
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [selected, setSelected] = useState<Asset | null>(null);
  const [editing, setEditing] = useState<Asset | null>(null);
  // Source for a cloned asset. Kept separate from `editing` on purpose: the form reads both for its
  // defaults, but only `editing` decides update-vs-create, so a duplicate always saves as a new record.
  const [duplicating, setDuplicating] = useState<Asset | null>(null);
  const [assetModalOpen, setAssetModalOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<Asset | null>(null);
  const [qrTarget, setQrTarget] = useState<Asset | null>(null);
  const [qrSvg, setQrSvg] = useState<string | null>(null);
  const [qrLoading, setQrLoading] = useState(false);
  const [quickAdd, setQuickAdd] = useState<'category' | 'location' | null>(null);
  const [quickAddIcon, setQuickAddIcon] = useState('');
  const [quickAddSaving, setQuickAddSaving] = useState(false);
  const { importOpen, openImport, closeImport } = useAssetImport();
  const [selectedCategoryId, setSelectedCategoryId] = useState('');
  const [formLocation, setFormLocation] = useState('');
  const [viewLocation, setViewLocation] = useState<string | null>(null);
  const [viewPersonId, setViewPersonId] = useState<string | null>(null);
  const [revealedFields, setRevealedFields] = useState<Record<string, string>>({});
  const [revealingKey, setRevealingKey] = useState<string | null>(null);
  const [bulkModal, setBulkModal] = useState<'status' | 'location' | null>(null);
  const [bulkSaving, setBulkSaving] = useState(false);
  const [pendingBulkOverride, setPendingBulkOverride] = useState<Partial<CreateAssetRequest & { status: AssetStatus }> | null>(null);
  const [batchQr, setBatchQr] = useState<{ asset: Asset; svg: string }[] | null>(null);
  const [batchQrLoading, setBatchQrLoading] = useState(false);
  const [batchAddOpen, setBatchAddOpen] = useState(false);
  const [exportImportMenuOpen, setExportImportMenuOpen] = useState(false);
  const qrLabelSettings = useAsyncData(api.qrLabelSettings, []);
  const openedAssetRef = useRef<string | null>(null);
  const assetsLoader = useMemo(
    () => () => api.assetsPaged({ search: debouncedSearch, status, location, teamId: team, owner, warranty, sort: sort?.key, desc: sort?.dir === -1, page, pageSize }),
    [debouncedSearch, status, location, team, owner, warranty, sort, page]
  );
  const assets = useAsyncData(assetsLoader, [assetsLoader]);
  const categories = useAsyncData(api.categories, []);
  const locations = useAsyncData(api.locations, []);
  const teams = useAsyncData(api.teams, []);
  const people = useAsyncData(() => api.people(), []);
  const groupCounts = useAsyncData(api.assetGroupCounts, []);
  const rows = useMemo(() => assets.data?.items ?? [], [assets.data]);
  const totalAssets = assets.data?.total ?? 0;
  const selectionResetKey = `${debouncedSearch}|${status}|${location}|${team}|${owner}|${warranty}|${page}`;
  const { selectedIds, selectedAssets, allOnPageSelected, toggleSelected, toggleSelectAllOnPage, clearSelection, keepOnly } = useAssetSelection(rows, selectionResetKey);
  const [browseReloadToken, setBrowseReloadToken] = useState(0);
  const reloadAssets = useCallback(async () => {
    setBrowseReloadToken(token => token + 1);
    await Promise.all([assets.reload(), groupCounts.reload()]);
  }, [assets, groupCounts]);

  const personGroups = useMemo<AssetGroup[]>(() => {
    const counts = groupCounts.data?.byPerson ?? {};
    return (people.data ?? [])
      .filter(person => counts[person.id] > 0)
      .map(person => ({ id: person.id, label: person.fullName, sublabel: [[person.jobTitle, person.teamName].filter(Boolean).join(' · '), t('assets.groupCount', { count: counts[person.id], noun: tPlural('count.assets', counts[person.id]) })].filter(Boolean).join(' · ') }))
      .sort((a, b) => (counts[b.id] ?? 0) - (counts[a.id] ?? 0));
  }, [people.data, groupCounts.data, t, tPlural]);
  const fetchPersonAssets = useCallback(async (personId: string) => {
    const result = await api.assetsPaged({ owner: personId, page: 1, pageSize: 100 });
    return { items: result.items, total: result.total };
  }, []);

  const categoryGroups = useMemo<AssetGroup[]>(() => {
    const counts = groupCounts.data?.byCategory ?? {};
    return (categories.data ?? [])
      .filter(cat => counts[cat.id] > 0)
      .map(cat => ({ id: cat.id, label: cat.name, sublabel: t('assets.groupCount', { count: counts[cat.id], noun: tPlural('count.assets', counts[cat.id]) }) }))
      .sort((a, b) => (counts[b.id] ?? 0) - (counts[a.id] ?? 0));
  }, [categories.data, groupCounts.data, t, tPlural]);
  const fetchCategoryAssets = useCallback(async (categoryId: string) => {
    const result = await api.assetsPaged({ categoryId, page: 1, pageSize: 100 });
    return { items: result.items, total: result.total };
  }, []);

  const statusGroups = useMemo<AssetGroup[]>(() => {
    const counts = groupCounts.data?.byStatus ?? {};
    return statuses
      .filter(item => item.value && (counts[item.value as AssetStatus] ?? 0) > 0)
      .map(item => ({ id: item.value, label: item.label, sublabel: t('assets.groupCount', { count: counts[item.value as AssetStatus] ?? 0, noun: tPlural('count.assets', counts[item.value as AssetStatus] ?? 0) }) }))
      .sort((a, b) => (counts[b.id as AssetStatus] ?? 0) - (counts[a.id as AssetStatus] ?? 0));
  }, [statuses, groupCounts.data, t, tPlural]);
  const fetchStatusAssets = useCallback(async (statusValue: string) => {
    const result = await api.assetsPaged({ status: statusValue as AssetStatus, page: 1, pageSize: 100 });
    return { items: result.items, total: result.total };
  }, []);

  async function handleSelectAssetFromTree(assetId: string) {
    try {
      const asset = await api.getAsset(assetId);
      setSelected(asset);
    } catch { /* asset lookup failed - selection just stays unchanged */ }
  }

  const categoryTypeLabels: Record<AssetCategoryType, string> = Object.fromEntries(categoryTypeValues.map(value => [value, t(`categoryType.${value}`)])) as Record<AssetCategoryType, string>;
  const locationTypeLabels: Record<LocationType, string> = Object.fromEntries(locationTypeValues.map(value => [value, t(`locationType.${value}`)])) as Record<LocationType, string>;
  const historyLoader = useMemo(
    () => () => (selected ? api.activityLog({ entityType: 'asset', entityId: selected.id, pageSize: 10 }) : Promise.resolve(null)),
    [selected]
  );
  const history = useAsyncData(historyLoader, [historyLoader]);
  const evidenceLoader = useMemo(
    () => () => (selected ? api.assetEvidence(selected.id) : Promise.resolve(null)),
    [selected]
  );
  const evidence = useAsyncData(evidenceLoader, [evidenceLoader]);
  const serviceTicketsLoader = useMemo(
    () => () => (selected ? api.assetServiceTickets(selected.id) : Promise.resolve(null)),
    [selected]
  );
  const serviceTickets = useAsyncData(serviceTicketsLoader, [serviceTicketsLoader]);

  const [serviceTicketModalOpen, setServiceTicketModalOpen] = useState(false);
  const [serviceTicketSaving, setServiceTicketSaving] = useState(false);
  const [completingTicket, setCompletingTicket] = useState<ServiceTicket | null>(null);
  const [completingSaving, setCompletingSaving] = useState(false);
  const [cancellingTicket, setCancellingTicket] = useState<ServiceTicket | null>(null);
  const [cancellingSaving, setCancellingSaving] = useState(false);
  const [exporting, setExporting] = useState(false);

  async function openServiceTicket(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selected) return;
    const form = new FormData(event.currentTarget);
    const vendor = String(form.get('vendor') ?? '').trim();
    if (!vendor) {
      setMessage({ type: 'error', text: t('serviceTickets.vendor') });
      return;
    }
    const description = String(form.get('description') ?? '').trim();
    const estimatedCostRaw = String(form.get('estimatedCost') ?? '').trim();
    const estimatedCost = estimatedCostRaw ? parseMoney(estimatedCostRaw) : null;
    const currency = String(form.get('currency') ?? '').trim();
    const slaDueAt = String(form.get('slaDueAt') ?? '').trim();
    setServiceTicketSaving(true);
    try {
      await api.openServiceTicket({
        assetId: selected.id,
        assetInspectionId: null,
        vendor,
        description: description || null,
        estimatedCost,
        currency: currency || null,
        slaDueAt: slaDueAt ? new Date(slaDueAt).toISOString() : null
      });
      setServiceTicketModalOpen(false);
      setMessage({ type: 'success', text: t('serviceTickets.title') });
      await Promise.all([serviceTickets.reload(), reloadAssets()]);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('common.saveError') });
    } finally {
      setServiceTicketSaving(false);
    }
  }

  async function completeServiceTicket(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!completingTicket) return;
    const form = new FormData(event.currentTarget);
    const actualCostRaw = String(form.get('actualCost') ?? '').trim();
    const actualCost = actualCostRaw ? parseMoney(actualCostRaw) : null;
    const resolution = String(form.get('resolution') ?? '').trim();
    const resultStatus = String(form.get('resultStatus') ?? '') as AssetStatus;
    setCompletingSaving(true);
    try {
      await api.completeServiceTicket(completingTicket.id, {
        actualCost,
        resolution: resolution || null,
        resultStatus
      });
      setCompletingTicket(null);
      await Promise.all([serviceTickets.reload(), reloadAssets()]);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('common.saveError') });
    } finally {
      setCompletingSaving(false);
    }
  }

  async function cancelServiceTicket() {
    if (!cancellingTicket) return;
    setCancellingSaving(true);
    try {
      await api.cancelServiceTicket(cancellingTicket.id, { resolution: null });
      setCancellingTicket(null);
      await Promise.all([serviceTickets.reload(), reloadAssets()]);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('common.saveError') });
    } finally {
      setCancellingSaving(false);
    }
  }

  const resultStatusOptions: AssetStatus[] = ['InStock', 'Damaged', 'Retired', 'Disposed'];
  const resultStatusLabels: Record<AssetStatus, string> = Object.fromEntries(resultStatusOptions.map(value => [value, t(`status.${value}`)])) as Record<AssetStatus, string>;

  // Export follows the screen: same filters, same order, and for CSV the same columns. Anything else
  // hands back a file that disagrees with the list the user was looking at when they clicked.
  function exportFilters() {
    return {
      search: debouncedSearch || undefined,
      status: status || undefined,
      location: location || undefined,
      teamId: team || undefined,
      unassignedOnly: owner === 'none',
      warrantyExpiring: warranty === 'expiring',
      sort: sort?.key,
      desc: sort?.dir === -1,
    };
  }

  function exportColumns() {
    const visible = readAssetColumns();
    return ['name', ...ASSET_COLUMNS.filter(column => visible[column.key]).map(column => column.key)].join(',');
  }

  async function downloadCsv() {
    if (exporting) return;
    setExporting(true);
    try {
      const blob = await api.downloadAssetsCsv({ ...exportFilters(), columns: exportColumns() });
      saveBlob(blob, 'assets.csv');
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('common.saveError') });
    } finally {
      setExporting(false);
    }
  }

  async function downloadJson() {
    if (exporting) return;
    setExporting(true);
    try {
      const blob = await api.downloadAssetsJson(exportFilters());
      saveBlob(blob, 'assets.json');
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('common.saveError') });
    } finally {
      setExporting(false);
    }
  }

  function buildUpdateBody(asset: Asset, overrides: Partial<CreateAssetRequest & { status: AssetStatus }> = {}): CreateAssetRequest & { status: AssetStatus } {
    return {
      name: asset.name,
      assetTag: asset.assetTag,
      serialNumber: asset.serialNumber,
      categoryId: asset.categoryId,
      location: asset.location,
      manufacturer: asset.manufacturer,
      model: asset.model,
      purchasePrice: asset.purchasePrice,
      currency: asset.currency,
      purchaseDate: asset.purchaseDate,
      warrantyUntil: asset.warrantyUntil,
      teamId: asset.teamId,
      customFields: asset.customFields,
      status: asset.status,
      ...overrides
    };
  }

  async function applyBulkUpdate(overrides: Partial<CreateAssetRequest & { status: AssetStatus }>) {
    setBulkSaving(true);
    let success = 0;
    const failedIds: string[] = [];
    for (const asset of selectedAssets) {
      try {
        await api.updateAsset(asset.id, buildUpdateBody(asset, overrides));
        success++;
      } catch {
        failedIds.push(asset.id);
      }
    }
    setBulkSaving(false);
    setBulkModal(null);
    const failed = failedIds.length;
    setMessage({ type: failed ? 'error' : 'success', text: t(failed ? 'assets.bulkFailedKept' : 'assets.bulkResult', { success, failed }) });
    keepOnly(failedIds);
    await reloadAssets();
  }

  function exportSelectedCsv() {
    const header = [t('assets.csvName'), t('assets.csvTag'), t('assets.csvSerialNumber'), t('assets.csvCategory'), t('assets.csvStatus'), t('assets.csvLocation'), t('assets.csvAssignee')];
    const rows = selectedAssets.map(asset => [asset.name, asset.assetTag, asset.serialNumber ?? '', asset.categoryName ?? '', t(`status.${asset.status}`), asset.location ?? '', asset.assignedPersonName ?? '']);
    const csv = [header, ...rows].map(row => row.map(csvCell).join(',')).join('\r\n');
    const blob = new Blob(['﻿' + csv], { type: 'text/csv' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = t('assets.csvFileName');
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  }

  async function openLabelSheet(assets: Asset[]) {
    setBatchQrLoading(true);
    const results: { asset: Asset; svg: string }[] = [];
    const failedTags: string[] = [];
    for (const asset of assets) {
      try {
        const svg = await api.assetQr(asset.id);
        results.push({ asset, svg });
      } catch {
        failedTags.push(asset.assetTag);
      }
    }
    setBatchQr(results);
    setBatchQrLoading(false);
    if (failedTags.length) {
      setMessage({ type: 'error', text: t('assets.qrBatchFailed', { count: failedTags.length, tags: failedTags.join(', ') }) });
    }
  }

  const openBatchQr = () => openLabelSheet(selectedAssets);

  // Zamknięcie pętli "przyjęcie dostawy": po utworzeniu partii od razu otwieramy arkusz etykiet,
  // bo naklejenie ich na kartony jest następną czynnością, a nie osobnym zadaniem na później.
  async function handleBatchCreated(created: Asset[]) {
    setBatchAddOpen(false);
    setMessage({ type: 'success', text: t('assets.batchCreated', { count: created.length }) });
    setPage(1);
    celebrate(t('celebration.assetAdded'));
    await reloadAssets();
    await openLabelSheet(created);
  }


  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);


  useEffect(() => {
    if (!openAssetId || !assets.data || openedAssetRef.current === openAssetId) return;
    openedAssetRef.current = openAssetId;
    const match = assets.data.items.find(asset => asset.id === openAssetId);
    if (match) setSelected(match);
    else api.getAsset(openAssetId).then(setSelected).catch(() => {});
  }, [assets.data, openAssetId]);

  useEffect(() => {
    setRevealedFields({});
  }, [selected?.id]);

  async function revealField(fieldKey: string) {
    if (!selected) return;
    setRevealingKey(fieldKey);
    try {
      const value = await api.revealAssetField(selected.id, fieldKey);
      setRevealedFields(current => ({ ...current, [fieldKey]: value }));
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('assets.revealFailed') });
    } finally {
      setRevealingKey(null);
    }
  }

  const selectedCategoryFields = useMemo(
    () => categories.data?.find(category => category.id === selectedCategoryId)?.fieldDefinitions ?? [],
    [categories.data, selectedCategoryId]
  );

  function openCreate() {
    setEditing(null);
    setDuplicating(null);
    setSelected(null);
    setSelectedCategoryId('');
    setFormLocation('');
    setAssetModalOpen(true);
  }

  // Wejście z palety komend (Ctrl+K) - otwiera pusty formularz i sprząta parametr, żeby odświeżenie
  // strony nie otwierało go po raz drugi. Ten sam wzorzec co ?addSelf=1 na stronie osób.
  useEffect(() => {
    if (searchParams.get('new') !== '1') return;
    openCreate();
    const next = new URLSearchParams(searchParams);
    next.delete('new');
    setSearchParams(next, { replace: true });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  function openEdit(asset: Asset) {
    setEditing(asset);
    setDuplicating(null);
    setSelected(null);
    setSelectedCategoryId(asset.categoryId);
    setFormLocation(asset.location ?? '');
    setAssetModalOpen(true);
  }

  /**
   * Opens the create form pre-filled from an existing asset.
   *
   * Everything that describes the model is copied; everything that identifies the individual unit -
   * tag, serial number, status, who holds it - is deliberately left blank, because those are exactly
   * the fields that must differ between two pieces of otherwise identical equipment.
   */
  function openDuplicate(asset: Asset) {
    setEditing(null);
    setDuplicating(asset);
    setSelected(null);
    setSelectedCategoryId(asset.categoryId);
    setFormLocation(asset.location ?? '');
    setAssetModalOpen(true);
  }

  function closeAssetModal() {
    setAssetModalOpen(false);
    setEditing(null);
    setDuplicating(null);
  }

  async function handleSave(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const categoryId = String(form.get('categoryId') ?? '');
    if (!categoryId) return setMessage({ type: 'error', text: t('assets.categoryRequired') });

    const rawPrice = String(form.get('purchasePrice') ?? '').trim();
    if (rawPrice && parseMoney(rawPrice) === null) return setMessage({ type: 'error', text: t('assets.invalidPrice') });

    const categoryFields = categories.data?.find(category => category.id === categoryId)?.fieldDefinitions ?? [];
    const customFields: Record<string, string> = {};
    for (const field of categoryFields) {
      if (field.fieldType === 'Boolean') {
        customFields[field.key] = form.get(`custom__${field.key}`) === 'on' ? 'true' : 'false';
      } else {
        const value = String(form.get(`custom__${field.key}`) ?? '').trim();
        if (value) customFields[field.key] = value;
      }
    }

    const body: CreateAssetRequest = {
      name: String(form.get('name') ?? '').trim(),
      assetTag: String(form.get('assetTag') ?? '').trim(),
      serialNumber: toNullable(String(form.get('serialNumber') ?? '')),
      categoryId,
      location: toNullable(String(form.get('location') ?? '')),
      manufacturer: toNullable(String(form.get('manufacturer') ?? '')),
      model: toNullable(String(form.get('model') ?? '')),
      purchasePrice: parseMoney(form.get('purchasePrice')),
      currency: toNullable(String(form.get('currency') ?? 'PLN')) ?? 'PLN',
      purchaseDate: toNullable(String(form.get('purchaseDate') ?? '')),
      warrantyUntil: toNullable(String(form.get('warrantyUntil') ?? '')),
      teamId: toNullable(String(form.get('teamId') ?? '')),
      customFields
    };

    if (!body.name || !body.assetTag) return setMessage({ type: 'error', text: t('assets.nameTagRequired') });
    setSaving(true);
    setMessage(null);
    try {
      await (editing
        ? api.updateAsset(editing.id, { ...body, status: String(form.get('status') ?? editing.status) as AssetStatus })
        : api.createAsset(body));
      setAssetModalOpen(false);
      setEditing(null);
      setDuplicating(null);
      setSelected(null);
      setMessage({ type: 'success', text: editing ? t('assets.saved') : t('assets.created') });
      if (!editing) {
        setPage(1);
        celebrate(t('celebration.assetAdded'));
      }
      await reloadAssets();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('assets.saveFailed') });
    } finally {
      setSaving(false);
    }
  }

  function openQuickAdd(kind: 'category' | 'location') {
    setQuickAddIcon('');
    setQuickAdd(kind);
  }

  async function saveQuickCategory(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const name = String(form.get('name') ?? '').trim();
    if (!name) return setMessage({ type: 'error', text: t('settings.categoryNameRequired') });
    setQuickAddSaving(true);
    try {
      const created = await api.createCategory({ name, type: String(form.get('type') ?? 'Physical') as AssetCategoryType, icon: toNullable(quickAddIcon) });
      setMessage({ type: 'success', text: t('settings.categorySaved') });
      setQuickAdd(null);
      await categories.reload();
      setSelectedCategoryId(created.id);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('settings.categorySaveFailed') });
    } finally {
      setQuickAddSaving(false);
    }
  }

  async function saveQuickLocation(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const name = String(form.get('name') ?? '').trim();
    if (!name) return setMessage({ type: 'error', text: t('locations.nameRequired') });
    setQuickAddSaving(true);
    try {
      const created = await api.createLocation({ name, type: String(form.get('type') ?? 'Room') as LocationType });
      setMessage({ type: 'success', text: t('locations.created') });
      setQuickAdd(null);
      await locations.reload();
      setFormLocation(created.fullPath);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('locations.createFailed') });
    } finally {
      setQuickAddSaving(false);
    }
  }

  async function openQr(asset: Asset) {
    setQrTarget(asset);
    setQrSvg(null);
    setQrLoading(true);
    try {
      setQrSvg(await api.assetQr(asset.id));
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('assets.qrFailed') });
      setQrTarget(null);
    } finally {
      setQrLoading(false);
    }
  }

  function svgToRasterBlob(svgString: string, mimeType: 'image/png' | 'image/jpeg'): Promise<Blob> {
    return new Promise((resolve, reject) => {
      const svgUrl = URL.createObjectURL(new Blob([svgString], { type: 'image/svg+xml;charset=utf-8' }));
      const img = new Image();
      img.onload = () => {
        const scale = 4;
        const canvas = document.createElement('canvas');
        canvas.width = img.naturalWidth * scale;
        canvas.height = img.naturalHeight * scale;
        const ctx = canvas.getContext('2d');
        URL.revokeObjectURL(svgUrl);
        if (!ctx) { reject(new Error('canvas unavailable')); return; }
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
        canvas.toBlob(blob => (blob ? resolve(blob) : reject(new Error('toBlob failed'))), mimeType, mimeType === 'image/jpeg' ? 0.92 : undefined);
      };
      img.onerror = () => { URL.revokeObjectURL(svgUrl); reject(new Error('svg load failed')); };
      img.src = svgUrl;
    });
  }

  async function downloadQr(format: 'svg' | 'png' | 'jpg') {
    if (!qrSvg || !qrTarget) return;
    if (format === 'svg') {
      saveBlob(new Blob([qrSvg], { type: 'image/svg+xml' }), `${qrTarget.assetTag}.svg`);
      return;
    }
    try {
      const blob = await svgToRasterBlob(qrSvg, format === 'png' ? 'image/png' : 'image/jpeg');
      saveBlob(blob, `${qrTarget.assetTag}.${format}`);
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('assets.qrFailed') });
    }
  }

  async function deleteAsset() {
    if (!deleteTarget) return;
    try {
      await api.deleteAsset(deleteTarget.id);
      setMessage({ type: 'success', text: t('assets.deleted') });
      setSelected(current => current?.id === deleteTarget.id ? null : current);
      setEditing(current => current?.id === deleteTarget.id ? null : current);
      setDeleteTarget(null);
      await reloadAssets();
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('assets.deleteFailed') });
      setDeleteTarget(null);
    }
  }

  if ((assets.isLoading && !assets.data) || (categories.isLoading && !categories.data) || (locations.isLoading && !locations.data)) return <LoadingState title={t('assets.loadingTitle')} description={t('assets.loadingDesc')} />;
  if (assets.error) return <ErrorState message={assets.error} onRetry={reloadAssets} />;

  const category = selected ? categories.data?.find(c => c.id === selected.categoryId) : null;

  // Descriptive defaults come from whichever asset is in play; identity fields below stay tied to
  // `editing` so a clone never inherits a tag, serial or status that must be unique to one unit.
  const prefill = editing ?? duplicating;

  return (
    <div className="pageStack">
      <PageHeader
        eyebrow={t('page.assets.eyebrow')}
        title={t('page.assets.title')}
        actions={
          <>
            <Button variant="secondary" iconOnly title={t('common.refresh')} aria-label={t('common.refresh')} onClick={reloadAssets} icon={<RefreshCw size={16} />} />
            <div className="menuPopover">
              <Button
                variant="secondary"
                iconOnly
                disabled={exporting}
                title={t('assets.exportImportMenu')}
                aria-label={t('assets.exportImportMenu')}
                onClick={() => setExportImportMenuOpen(open => !open)}
                icon={<FileSpreadsheet size={16} />}
              />
              {exportImportMenuOpen ? (
                <>
                  <button type="button" className="menuPopover__scrim" aria-label={t('common.close')} onClick={() => setExportImportMenuOpen(false)} />
                  <div className="menuPopover__panel">
                    <button type="button" className="menuPopover__item" disabled={exporting} onClick={() => { setExportImportMenuOpen(false); void downloadCsv(); }}><Download size={15} />{t('assets.exportCsv')}</button>
                    <button type="button" className="menuPopover__item" disabled={exporting} onClick={() => { setExportImportMenuOpen(false); void downloadJson(); }}><Download size={15} />{t('assets.exportJson')}</button>
                    <button type="button" className="menuPopover__item" onClick={() => { setExportImportMenuOpen(false); openImport(); }}><Upload size={15} />{t('assets.import')}</button>
                  </div>
                </>
              ) : null}
            </div>
            <Button variant="secondary" iconOnly title={t('assets.batchAdd')} aria-label={t('assets.batchAdd')} onClick={() => setBatchAddOpen(true)} icon={<Layers size={16} />} />
            <Button iconOnly title={t('assets.add')} aria-label={t('assets.add')} onClick={openCreate} icon={<Plus size={16} />} />
          </>
        }
      />

      {message && (
        <div className="toastStack" aria-live="polite">
          <div className={`toast toast--${message.type}`}>{message.text}</div>
        </div>
      )}

      <AssetsToolbar
        selectedCount={selectedIds.size}
        batchQrLoading={batchQrLoading}
        onBulkStatus={() => setBulkModal('status')}
        onBulkLocation={() => setBulkModal('location')}
        onExportSelected={exportSelectedCsv}
        onBatchQr={openBatchQr}
        onClearSelection={clearSelection}
        owner={owner}
        setOwner={setOwner}
        warranty={warranty}
        setWarranty={setWarranty}
        search={search}
        setSearch={setSearch}
        status={status}
        setStatus={setStatus}
        location={location}
        setLocation={setLocation}
        team={team}
        setTeam={setTeam}
        statuses={statuses}
        locations={locations.data ?? []}
        teams={teams.data ?? []}
      />

      <Card>
        <div className="tabs assetViewTabs" role="tablist" aria-label={t('assets.listTitle')}>
          <ViewTab active={viewMode === 'list'} onClick={() => setViewMode('list')} icon={<List size={16} />} label={t('assets.viewList')} />
          <ViewTab active={viewMode === 'location'} onClick={() => setViewMode('location')} icon={<Building2 size={16} />} label={t('assets.browseByLocation')} />
          <ViewTab active={viewMode === 'person'} onClick={() => setViewMode('person')} icon={<Users size={16} />} label={t('assets.viewPerson')} />
          <ViewTab active={viewMode === 'status'} onClick={() => setViewMode('status')} icon={<CircleDot size={16} />} label={t('assets.viewStatus')} />
          <ViewTab active={viewMode === 'category'} onClick={() => setViewMode('category')} icon={<Tag size={16} />} label={t('assets.viewCategory')} />
        </div>

        {viewMode === 'location' && (
          <LocationAssetBrowser locations={locations.data ?? []} categories={categories.data ?? []} onSelectAsset={handleSelectAssetFromTree} reloadToken={browseReloadToken} />
        )}
        {viewMode === 'person' && (
          <GroupedAssetBrowser groups={personGroups} icon={<Users size={16} />} categories={categories.data ?? []} fetchGroupAssets={fetchPersonAssets} onSelectAsset={handleSelectAssetFromTree} reloadToken={browseReloadToken} />
        )}
        {viewMode === 'status' && (
          <GroupedAssetBrowser groups={statusGroups} icon={<CircleDot size={16} />} categories={categories.data ?? []} fetchGroupAssets={fetchStatusAssets} onSelectAsset={handleSelectAssetFromTree} reloadToken={browseReloadToken} />
        )}
        {viewMode === 'category' && (
          <GroupedAssetBrowser groups={categoryGroups} icon={<Tag size={16} />} categories={categories.data ?? []} fetchGroupAssets={fetchCategoryAssets} onSelectAsset={handleSelectAssetFromTree} reloadToken={browseReloadToken} />
        )}
        {viewMode === 'list' && (
          <AssetsList
            rows={rows}
            categories={categories.data ?? []}
            statusSettingByKey={statusSettingByKey}
            isLoading={assets.isLoading}
            totalAssets={totalAssets}
            page={page}
            pageSize={pageSize}
            filtersActive={hasFilters}
            onClearFilters={clearFilters}
            onCreate={openCreate}
            onSelect={setSelected}
            onViewPerson={setViewPersonId}
            onViewLocation={setViewLocation}
            selectedIds={selectedIds}
            allOnPageSelected={allOnPageSelected}
            onToggleSelected={toggleSelected}
            onToggleSelectAll={toggleSelectAllOnPage}
            sort={sort}
            onToggleSort={toggleSort}
            onPageChange={setPage}
          />
        )}
      </Card>

      <AssetDetailPanel
        selected={selected}
        categoryIcon={category?.icon}
        statusSettingByKey={statusSettingByKey}
        onClose={() => setSelected(null)}
        onQr={openQr}
        onEdit={openEdit}
        onDuplicate={openDuplicate}
        onDelete={asset => { setDeleteTarget(asset); setSelected(null); }}
        onViewPerson={setViewPersonId}
        onViewLocation={setViewLocation}
        revealedFields={revealedFields}
        revealingKey={revealingKey}
        onRevealField={revealField}
        evidence={evidence.data}
        evidenceLoading={evidence.isLoading}
        serviceTickets={serviceTickets.data}
        serviceTicketsLoading={serviceTickets.isLoading}
        onOpenServiceTicket={() => setServiceTicketModalOpen(true)}
        onCompleteServiceTicket={setCompletingTicket}
        onCancelServiceTicket={setCancellingTicket}
        history={history.data?.items}
        historyLoading={history.isLoading}
      />

      <Modal
        open={assetModalOpen}
        title={editing ? t('assets.editTitle') : t('assets.addTitle')}
        onClose={closeAssetModal}
        width="wide"
      >
        <form className="formGrid" onSubmit={handleSave} key={editing?.id ?? (duplicating ? `dup-${duplicating.id}` : 'new-asset')}>
          <div className="formSectionTitle">{t('assets.identification')}</div>
          <Field label={t('assets.nameLabel')}><TextInput name="name" defaultValue={prefill?.name ?? ''} required /></Field>
          <Field label={t('assets.tagLabel')}><TextInput name="assetTag" defaultValue={editing?.assetTag ?? ''} required /></Field>
          <Field label={t('assets.categoryLabel')}>
            <div className="fieldWithAdd">
              <SelectInput name="categoryId" value={selectedCategoryId} onChange={event => setSelectedCategoryId(event.target.value)} required>
                <option value="">{t('assets.chooseCategory')}</option>
                {categories.data?.map(category => <option key={category.id} value={category.id}>{category.name}</option>)}
              </SelectInput>
              <button type="button" className="iconButton iconButton--add" aria-label={t('settings.addCategory')} title={t('settings.addCategory')} onClick={() => openQuickAdd('category')}><Plus size={18} /></button>
            </div>
          </Field>
          {editing && (
            <Field label={t('assets.statusLabel')}>
              <SelectInput name="status" defaultValue={editing.status}>
                {statuses.filter(item => item.value).map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
              </SelectInput>
            </Field>
          )}
          <Field label={t('assets.serialNumberLabel')}><TextInput name="serialNumber" defaultValue={editing?.serialNumber ?? ''} /></Field>
          <Field label={t('assets.locationLabel')}>
            <div className="fieldWithAdd">
              <SelectInput name="location" value={formLocation} onChange={event => setFormLocation(event.target.value)}>
                <option value="">{t('assets.noLocationOption')}</option>
                {locations.data?.map(item => <option key={item.id} value={item.fullPath}>{item.fullPath}</option>)}
              </SelectInput>
              <button type="button" className="iconButton iconButton--add" aria-label={t('locations.add')} title={t('locations.add')} onClick={() => openQuickAdd('location')}><Plus size={18} /></button>
            </div>
          </Field>
          <Field label={t('assets.teamLabel')}>
            <SelectInput name="teamId" defaultValue={prefill?.teamId ?? ''}>
              <option value="">{t('assets.noTeamOption')}</option>
              {teams.data?.map(team => <option key={team.id} value={team.id}>{team.name}</option>)}
            </SelectInput>
          </Field>

          <div className="formSectionTitle">{t('assets.descAndDates')}</div>
          <Field label={t('assets.manufacturerLabel')}><TextInput name="manufacturer" defaultValue={prefill?.manufacturer ?? ''} /></Field>
          <Field label={t('assets.modelLabel')}><TextInput name="model" defaultValue={prefill?.model ?? ''} /></Field>
          <Field label={t('assets.purchasePriceLabel')}><TextInput name="purchasePrice" inputMode="decimal" defaultValue={prefill?.purchasePrice ?? ''} /></Field>
          <Field label={t('assets.currencyLabel')}><TextInput name="currency" defaultValue={prefill?.currency ?? 'PLN'} maxLength={3} /></Field>
          <Field label={t('assets.purchaseDateLabel')}><TextInput name="purchaseDate" type="date" defaultValue={prefill?.purchaseDate ?? ''} /></Field>
          <Field label={t('assets.warrantyUntilLabel')}><TextInput name="warrantyUntil" type="date" defaultValue={prefill?.warrantyUntil ?? ''} /></Field>

          {selectedCategoryFields.length > 0 && (
            <>
              <div className="formSectionTitle">{t('assets.customFieldsSection')}</div>
              {selectedCategoryFields.map(field => (
                <Field key={field.id} label={field.required ? `${field.label} *` : field.label}>
                  {field.fieldType === 'Boolean' ? (
                    <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <input type="checkbox" name={`custom__${field.key}`} defaultChecked={prefill?.customFields?.[field.key] === 'true'} />
                    </label>
                  ) : field.fieldType === 'Select' ? (
                    <SelectInput name={`custom__${field.key}`} defaultValue={prefill?.customFields?.[field.key] ?? ''} required={field.required}>
                      <option value="">{t('assets.customFieldChoose')}</option>
                      {field.options.map(option => <option key={option} value={option}>{option}</option>)}
                    </SelectInput>
                  ) : (
                    <TextInput
                      name={`custom__${field.key}`}
                      type={field.fieldType === 'Number' ? 'number' : field.fieldType === 'Date' ? 'date' : field.fieldType === 'Sensitive' ? 'password' : 'text'}
                      defaultValue={field.fieldType === 'Sensitive' ? '' : prefill?.customFields?.[field.key] ?? ''}
                      placeholder={field.fieldType === 'Sensitive' && editing ? t('assets.sensitiveKeepPlaceholder') : undefined}
                      required={field.required && !(field.fieldType === 'Sensitive' && editing)}
                    />
                  )}
                </Field>
              ))}
            </>
          )}

          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={closeAssetModal}>{t('common.cancel')}</Button>
            <Button disabled={saving} icon={editing ? <Pencil size={16} /> : <Plus size={16} />}>
              {saving ? t('common.saving') : editing ? t('assets.save') : t('assets.add')}
            </Button>
          </div>
        </form>
      </Modal>

      <Modal open={serviceTicketModalOpen} title={t('serviceTickets.open')} onClose={() => setServiceTicketModalOpen(false)}>
        <form className="formGrid" onSubmit={openServiceTicket}>
          <Field label={`${t('serviceTickets.vendor')} *`}><TextInput name="vendor" required /></Field>
          <Field label={t('serviceTickets.description')}><TextArea name="description" rows={3} /></Field>
          <Field label={t('serviceTickets.estimatedCost')}><TextInput name="estimatedCost" type="number" inputMode="decimal" /></Field>
          <Field label={t('serviceTickets.currency')}><TextInput name="currency" maxLength={3} /></Field>
          <Field label={t('serviceTickets.slaDueAt')}><TextInput name="slaDueAt" type="date" /></Field>
          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={() => setServiceTicketModalOpen(false)}>{t('common.cancel')}</Button>
            <Button disabled={serviceTicketSaving}>{serviceTicketSaving ? t('common.saving') : t('serviceTickets.open')}</Button>
          </div>
        </form>
      </Modal>

      <Modal open={!!completingTicket} title={t('serviceTickets.complete')} onClose={() => setCompletingTicket(null)}>
        <form className="formGrid" onSubmit={completeServiceTicket}>
          <Field label={t('serviceTickets.actualCost')}><TextInput name="actualCost" type="number" inputMode="decimal" /></Field>
          <Field label={t('serviceTickets.resolution')}><TextArea name="resolution" rows={3} /></Field>
          <Field label={t('serviceTickets.resultStatus')}>
            <SelectInput name="resultStatus" defaultValue="InStock" required>
              {resultStatusOptions.map(value => <option key={value} value={value}>{resultStatusLabels[value]}</option>)}
            </SelectInput>
          </Field>
          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={() => setCompletingTicket(null)}>{t('common.cancel')}</Button>
            <Button disabled={completingSaving}>{completingSaving ? t('common.saving') : t('serviceTickets.complete')}</Button>
          </div>
        </form>
      </Modal>

      <ConfirmDialog
        open={!!cancellingTicket}
        title={t('serviceTickets.confirmCancelTitle')}
        description={t('serviceTickets.confirmCancelBody')}
        confirmLabel={t('serviceTickets.cancel')}
        confirmDisabled={cancellingSaving}
        onConfirm={cancelServiceTicket}
        onClose={() => setCancellingTicket(null)}
      />

      <ConfirmDialog
        open={!!deleteTarget}
        title={t('assets.deleteConfirmTitle')}
        description={t('assets.deleteConfirmDesc')}
        confirmLabel={t('assets.delete')}
        onConfirm={deleteAsset}
        onClose={() => setDeleteTarget(null)}
      />

      <Modal open={!!qrTarget} title={qrTarget ? `${t('assets.qrCode')} - ${qrTarget.assetTag}` : t('assets.qrCode')} onClose={() => setQrTarget(null)}>
        {qrLoading ? <p className="muted">{t('assets.generatingQr')}</p> : qrSvg ? (
          <div className="qrPreview">
            <div className="qrPreview__image"><img src={`data:image/svg+xml;charset=utf-8,${encodeURIComponent(qrSvg)}`} alt="QR" /></div>
            <div style={{ display: 'flex', gap: '8px', flexWrap: 'wrap', justifyContent: 'center' }}>
              <Button onClick={() => void downloadQr('png')} icon={<Download size={16} />}>{t('assets.downloadPng')}</Button>
              <Button onClick={() => void downloadQr('jpg')} icon={<Download size={16} />}>{t('assets.downloadJpg')}</Button>
              <Button onClick={() => void downloadQr('svg')} icon={<Download size={16} />}>{t('assets.downloadSvg')}</Button>
            </div>
          </div>
        ) : null}
      </Modal>

      <Modal open={quickAdd === 'category'} title={t('settings.addCategoryTitle')} onClose={() => setQuickAdd(null)}>
        <form className="formGrid" onSubmit={saveQuickCategory}>
          <Field label={t('settings.nameLabel')}><TextInput name="name" required /></Field>
          <Field label={t('settings.typeLabel')}><SelectInput name="type" defaultValue="Physical">{categoryTypeValues.map(value => <option value={value} key={value}>{categoryTypeLabels[value]}</option>)}</SelectInput></Field>
          <Field label={t('settings.iconLabel')}><IconPicker value={quickAddIcon} onChange={setQuickAddIcon} /></Field>
          <div className="formActions formActions--split"><Button type="button" variant="ghost" onClick={() => setQuickAdd(null)}>{t('common.cancel')}</Button><Button disabled={quickAddSaving}>{quickAddSaving ? t('common.saving') : t('settings.save')}</Button></div>
        </form>
      </Modal>

      <Modal open={quickAdd === 'location'} title={t('locations.add')} onClose={() => setQuickAdd(null)}>
        <form className="formGrid" onSubmit={saveQuickLocation}>
          <Field label={t('locations.nameLabel')}><TextInput name="name" required placeholder={t('locations.namePlaceholder')} /></Field>
          <Field label={t('locations.typeLabel')}><SelectInput name="type" defaultValue="Room">{locationTypeValues.map(value => <option value={value} key={value}>{locationTypeLabels[value]}</option>)}</SelectInput></Field>
          <div className="formActions formActions--split"><Button type="button" variant="ghost" onClick={() => setQuickAdd(null)}>{t('common.cancel')}</Button><Button disabled={quickAddSaving}>{quickAddSaving ? t('common.saving') : t('locations.add')}</Button></div>
        </form>
      </Modal>

      <ImportModal
        open={importOpen}
        entity="assets"
        existingKeys={rows.map(item => item.assetTag.toLowerCase())}
        categories={categories.data ?? []}
        locations={locations.data ?? []}
        onClose={closeImport}
        onDone={reloadAssets}
      />

      {viewLocation && <LocationInventoryModal locationPath={viewLocation} onClose={() => setViewLocation(null)} />}
      {viewPersonId && <PersonPreviewModal personId={viewPersonId} onClose={() => setViewPersonId(null)} />}

      <Modal open={bulkModal === 'status'} title={t('assets.bulkChangeStatusTitle')} onClose={() => setBulkModal(null)}>
        <form className="formGrid" onSubmit={event => { event.preventDefault(); const form = new FormData(event.currentTarget); const overrides = { status: String(form.get('status')) as AssetStatus }; if (selectedAssets.length > 1) setPendingBulkOverride(overrides); else void applyBulkUpdate(overrides); }}>
          <Field label={t('assets.statusLabel')}>
            <SelectInput name="status" defaultValue="InStock">
              {statuses.filter(item => item.value).map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
            </SelectInput>
          </Field>
          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={() => setBulkModal(null)}>{t('common.cancel')}</Button>
            <Button disabled={bulkSaving}>{bulkSaving ? t('common.saving') : t('assets.bulkApply')}</Button>
          </div>
        </form>
      </Modal>

      <Modal open={bulkModal === 'location'} title={t('assets.bulkMoveTitle')} onClose={() => setBulkModal(null)}>
        <form className="formGrid" onSubmit={event => { event.preventDefault(); const form = new FormData(event.currentTarget); const overrides = { location: toNullable(String(form.get('location') ?? '')) }; if (selectedAssets.length > 1) setPendingBulkOverride(overrides); else void applyBulkUpdate(overrides); }}>
          <Field label={t('assets.locationLabel')}>
            <SelectInput name="location" defaultValue="">
              <option value="">{t('assets.noLocationOption')}</option>
              {locations.data?.map(item => <option key={item.id} value={item.fullPath}>{item.fullPath}</option>)}
            </SelectInput>
          </Field>
          <div className="formActions formActions--split">
            <Button type="button" variant="ghost" onClick={() => setBulkModal(null)}>{t('common.cancel')}</Button>
            <Button disabled={bulkSaving}>{bulkSaving ? t('common.saving') : t('assets.bulkApply')}</Button>
          </div>
        </form>
      </Modal>

      <ConfirmDialog
        open={!!pendingBulkOverride}
        title={t('assets.bulkConfirmTitle')}
        description={t('assets.bulkConfirmDesc', { count: selectedAssets.length })}
        confirmLabel={t('assets.bulkApply')}
        onConfirm={() => { const overrides = pendingBulkOverride; setPendingBulkOverride(null); if (overrides) void applyBulkUpdate(overrides); }}
        onClose={() => setPendingBulkOverride(null)}
      />

      <LabelSheetModal labels={batchQr} defaultSize={qrLabelSettings.data?.format as LabelSize | undefined} onClose={() => setBatchQr(null)} />

      <BatchAddModal
        open={batchAddOpen}
        onClose={() => setBatchAddOpen(false)}
        categories={categories.data ?? []}
        locations={locations.data ?? []}
        teams={teams.data ?? []}
        onCreated={created => void handleBatchCreated(created)}
        onError={text => setMessage({ type: 'error', text })}
      />
    </div>
  );
}

function ViewTab({ active, onClick, icon, label }: { active: boolean; onClick(): void; icon: React.ReactNode; label: string }) {
  return (
    <button type="button" role="tab" aria-selected={active} className={active ? 'tab tab--active' : 'tab'} onClick={onClick}>
      <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>{icon}{label}</span>
    </button>
  );
}
