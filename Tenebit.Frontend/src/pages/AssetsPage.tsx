import { ArrowDown, ArrowUp, Building2, CircleDot, Download, Eye, FileSpreadsheet, List, Pencil, Plus, Printer, QrCode, RefreshCw, Tag, Trash2, Upload, Users, X } from 'lucide-react';
import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { api } from '../api/endpoints';
import { Button } from '../components/Button';
import { Card } from '../components/Card';
import { EvidenceGallery } from '../components/Evidence';
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
import { Pagination } from '../components/Pagination';
import { SlidePanel } from '../components/SlidePanel';
import { EmptyState, ErrorState, LoadingState } from '../components/StateViews';
import { StatusBadge } from '../components/StatusBadge';
import { useAsyncData } from '../hooks/useAsyncData';
import { useDebouncedValue } from '../hooks/useDebouncedValue';
import type { Asset, AssetCategoryType, AssetStatus, CreateAssetRequest, LocationType, ServiceTicket, ServiceTicketStatus } from '../types/domain';
import { csvCell, formatDate, formatDateTime, formatMoney, toNullable } from '../utils/format';
import { activityLabel, assetStatusValues, categoryTypeValues, locationTypeValues } from '../utils/labels';
import { CategoryIcon } from '../utils/categoryIcons';
import { useI18n } from '../i18n/I18nProvider';
import { useCelebration } from '../celebration/CelebrationProvider';
import { useAuth } from '../auth/AuthProvider';

const pageSize = 25;
type ViewMode = 'list' | 'location' | 'person' | 'status' | 'category';
const assetsViewStorageKey = (email: string) => `tenebit_assets_view_${email}`;

type SortKey = 'name' | 'assetTag' | 'status' | 'person' | 'location' | 'value' | 'warranty';

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
  const statuses: { value: AssetStatus | ''; label: string }[] = [
    { value: '', label: t('assets.allStatuses') },
    ...(statusSettings.data?.length
      ? [...statusSettings.data].filter(item => item.isEnabled).sort((a, b) => a.sortOrder - b.sortOrder).map(item => ({ value: item.statusKey, label: item.label }))
      : assetStatusValues.map(value => ({ value, label: t(`status.${value}`) })))
  ];
  const [searchParams, setSearchParams] = useSearchParams();
  const [search, setSearch] = useState(searchParams.get('search') ?? '');
  const [status, setStatus] = useState<AssetStatus | ''>((searchParams.get('status') as AssetStatus | null) ?? '');
  const [location, setLocation] = useState(searchParams.get('location') ?? '');
  const [team, setTeam] = useState(searchParams.get('team') ?? '');
  const [owner, setOwner] = useState(searchParams.get('owner') ?? '');
  const [warranty, setWarranty] = useState(searchParams.get('warranty') ?? '');
  const [page, setPage] = useState(1);
  const [saving, setSaving] = useState(false);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [selected, setSelected] = useState<Asset | null>(null);
  const [editing, setEditing] = useState<Asset | null>(null);
  const [assetModalOpen, setAssetModalOpen] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<Asset | null>(null);
  const [qrTarget, setQrTarget] = useState<Asset | null>(null);
  const [qrSvg, setQrSvg] = useState<string | null>(null);
  const [qrLoading, setQrLoading] = useState(false);
  const [quickAdd, setQuickAdd] = useState<'category' | 'location' | null>(null);
  const [quickAddIcon, setQuickAddIcon] = useState('');
  const [quickAddSaving, setQuickAddSaving] = useState(false);
  const [importOpen, setImportOpen] = useState(false);
  const [selectedCategoryId, setSelectedCategoryId] = useState('');
  const [formLocation, setFormLocation] = useState('');
  const [viewLocation, setViewLocation] = useState<string | null>(null);
  const [viewPersonId, setViewPersonId] = useState<string | null>(null);
  const [revealedFields, setRevealedFields] = useState<Record<string, string>>({});
  const [revealingKey, setRevealingKey] = useState<string | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [bulkModal, setBulkModal] = useState<'status' | 'location' | null>(null);
  const [bulkSaving, setBulkSaving] = useState(false);
  const [pendingBulkOverride, setPendingBulkOverride] = useState<Partial<CreateAssetRequest & { status: AssetStatus }> | null>(null);
  const [batchQr, setBatchQr] = useState<{ asset: Asset; svg: string }[] | null>(null);
  const [batchQrLoading, setBatchQrLoading] = useState(false);
  const openedAssetRef = useRef<string | null>(null);
  const debouncedSearch = useDebouncedValue(search.trim(), 320);

  const [sort, setSort] = useState<{ key: SortKey; dir: 1 | -1 } | null>(null);
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
  const reloadAssets = useCallback(async () => { await Promise.all([assets.reload(), groupCounts.reload()]); }, [assets, groupCounts]);

  const personGroups = useMemo<AssetGroup[]>(() => {
    const counts = groupCounts.data?.byPerson ?? {};
    return (people.data ?? [])
      .filter(person => counts[person.id] > 0)
      .map(person => ({ id: person.id, label: person.fullName, sublabel: [[person.jobTitle, person.teamName].filter(Boolean).join(' · '), t('assets.groupCount', { count: counts[person.id], noun: tPlural('count.assets', counts[person.id]) })].filter(Boolean).join(' · ') }))
      .sort((a, b) => (counts[b.id] ?? 0) - (counts[a.id] ?? 0));
  }, [people.data, groupCounts.data, t, tPlural]);
  const fetchPersonAssets = useCallback(async (personId: string) => {
    const workspace = await api.personWorkspace(personId);
    const items = (workspace?.assets ?? []).map(asset => ({ id: asset.id, name: asset.name, assetTag: asset.assetTag, categoryName: asset.categoryName }));
    return { items, total: items.length };
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
    return { items: result.items.map(asset => ({ id: asset.id, name: asset.name, assetTag: asset.assetTag, status: asset.status })), total: result.total };
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
    return { items: result.items.map(asset => ({ id: asset.id, name: asset.name, assetTag: asset.assetTag, categoryName: asset.categoryName })), total: result.total };
  }, []);

  async function handleSelectAssetFromTree(assetId: string) {
    try {
      const asset = await api.getAsset(assetId);
      setSelected(asset);
    } catch {}
  }

  function toggleSort(key: SortKey) {
    setSort(current => (current?.key === key ? (current.dir === 1 ? { key, dir: -1 } : null) : { key, dir: 1 }));
    setPage(1);
  }

  function SortableTh({ sortKey, children, align }: { sortKey: SortKey; children: React.ReactNode; align?: 'right' }) {
    const active = sort?.key === sortKey;
    return (
      <th style={align === 'right' ? { textAlign: 'right' } : undefined} aria-sort={active ? (sort.dir === 1 ? 'ascending' : 'descending') : undefined}>
        <button type="button" className={active ? 'thSort thSort--active' : 'thSort'} onClick={() => toggleSort(sortKey)}>
          {children}
          {active ? (sort.dir === 1 ? <ArrowUp size={12} /> : <ArrowDown size={12} />) : null}
        </button>
      </th>
    );
  }
  const categoryTypeLabels: Record<AssetCategoryType, string> = Object.fromEntries(categoryTypeValues.map(value => [value, t(`categoryType.${value}`)])) as Record<AssetCategoryType, string>;
  const locationTypeLabels: Record<LocationType, string> = Object.fromEntries(locationTypeValues.map(value => [value, t(`locationType.${value}`)])) as Record<LocationType, string>;
  const selectedAssets = useMemo(() => rows.filter(asset => selectedIds.has(asset.id)), [rows, selectedIds]);
  const allOnPageSelected = rows.length > 0 && rows.every(asset => selectedIds.has(asset.id));
  const historyLoader = useMemo(
    () => () => (selected ? api.activityLog({ entityType: 'asset', entityId: selected.id, pageSize: 10 }) : Promise.resolve(null)),
    [selected?.id]
  );
  const history = useAsyncData(historyLoader, [historyLoader]);
  const evidenceLoader = useMemo(
    () => () => (selected ? api.assetEvidence(selected.id) : Promise.resolve(null)),
    [selected?.id]
  );
  const evidence = useAsyncData(evidenceLoader, [evidenceLoader]);
  const serviceTicketsLoader = useMemo(
    () => () => (selected ? api.assetServiceTickets(selected.id) : Promise.resolve(null)),
    [selected?.id]
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

  function ticketStatusClass(status: ServiceTicketStatus): string {
    if (status === 'Completed') return 'status status--InStock';
    if (status === 'Cancelled') return 'status status--Damaged';
    return 'status status--InService';
  }

  const serviceTicketStatusLabels: Record<ServiceTicketStatus, string> = {
    Open: t('serviceTickets.status.Open'),
    InProgress: t('serviceTickets.status.InProgress'),
    WaitingForParts: t('serviceTickets.status.WaitingForParts'),
    Completed: t('serviceTickets.status.Completed'),
    Cancelled: t('serviceTickets.status.Cancelled')
  };
  const resultStatusOptions: AssetStatus[] = ['InStock', 'Damaged', 'Retired', 'Disposed'];
  const resultStatusLabels: Record<AssetStatus, string> = Object.fromEntries(resultStatusOptions.map(value => [value, t(`status.${value}`)])) as Record<AssetStatus, string>;

  async function downloadCsv() {
    if (exporting) return;
    setExporting(true);
    try {
      const blob = await api.downloadAssetsCsv({ search: debouncedSearch, status: status || undefined, location: location || undefined });
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
      const blob = await api.downloadAssetsJson({ search: debouncedSearch, status: status || undefined, location: location || undefined });
      saveBlob(blob, 'assets.json');
    } catch (error) {
      setMessage({ type: 'error', text: error instanceof Error ? error.message : t('common.saveError') });
    } finally {
      setExporting(false);
    }
  }

  function toggleSelected(id: string) {
    setSelectedIds(current => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id); else next.add(id);
      return next;
    });
  }

  function toggleSelectAllOnPage() {
    setSelectedIds(current => {
      const next = new Set(current);
      if (allOnPageSelected) rows.forEach(asset => next.delete(asset.id));
      else rows.forEach(asset => next.add(asset.id));
      return next;
    });
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
    setSelectedIds(new Set(failedIds));
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

  async function openBatchQr() {
    setBatchQrLoading(true);
    const results: { asset: Asset; svg: string }[] = [];
    const failedTags: string[] = [];
    for (const asset of selectedAssets) {
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

  useEffect(() => {
    const params = new URLSearchParams();
    if (debouncedSearch) params.set('search', debouncedSearch);
    if (status) params.set('status', status);
    if (location) params.set('location', location);
    if (team) params.set('team', team);
    if (owner) params.set('owner', owner);
    if (warranty) params.set('warranty', warranty);
    setSearchParams(params, { replace: true });
    setPage(1);
    setSelectedIds(new Set());
  }, [debouncedSearch, location, team, setSearchParams, status, owner, warranty]);

  useEffect(() => {
    if (!message) return;
    const timeout = window.setTimeout(() => setMessage(null), message.type === 'success' ? 3500 : 6500);
    return () => window.clearTimeout(timeout);
  }, [message]);

  useEffect(() => {
    setSelectedIds(new Set());
  }, [page]);

  useEffect(() => {
    const openAssetId = searchParams.get('openAssetId');
    if (!openAssetId || !assets.data || openedAssetRef.current === openAssetId) return;
    openedAssetRef.current = openAssetId;
    const match = assets.data.items.find(asset => asset.id === openAssetId);
    if (match) setSelected(match);
    else api.getAsset(openAssetId).then(setSelected).catch(() => {});
  }, [assets.data, searchParams]);

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
    setSelected(null);
    setSelectedCategoryId('');
    setFormLocation('');
    setAssetModalOpen(true);
  }

  function openEdit(asset: Asset) {
    setEditing(asset);
    setSelected(null);
    setSelectedCategoryId(asset.categoryId);
    setFormLocation(asset.location ?? '');
    setAssetModalOpen(true);
  }

  function closeAssetModal() {
    setAssetModalOpen(false);
    setEditing(null);
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

  if ((assets.isLoading && !assets.data) || categories.isLoading || locations.isLoading) return <LoadingState title={t('assets.loadingTitle')} description={t('assets.loadingDesc')} />;
  if (assets.error) return <ErrorState message={assets.error} onRetry={reloadAssets} />;

  const category = selected ? categories.data?.find(c => c.id === selected.categoryId) : null;

  return (
    <div className="pageStack">
      <PageHeader
        eyebrow={t('page.assets.eyebrow')}
        title={t('page.assets.title')}
        actions={
          <>
            <Button variant="secondary" onClick={reloadAssets} icon={<RefreshCw size={16} />}>
              {t('common.refresh')}
            </Button>
            <Button variant="secondary" disabled={exporting} onClick={() => void downloadCsv()} icon={<Download size={16} />}>
              {exporting ? t('common.loading') : t('assets.exportCsv')}
            </Button>
            <Button variant="secondary" disabled={exporting} onClick={() => void downloadJson()} icon={<Download size={16} />}>
              {exporting ? t('common.loading') : t('assets.exportJson')}
            </Button>
            <Button variant="secondary" onClick={() => setImportOpen(true)} icon={<Upload size={16} />}>
              {t('assets.import')}
            </Button>
            <Button onClick={openCreate} icon={<Plus size={16} />}>
              {t('assets.add')}
            </Button>
          </>
        }
      />

      {message && (
        <div className="toastStack" aria-live="polite">
          <div className={`toast toast--${message.type}`}>{message.text}</div>
        </div>
      )}

      {selectedIds.size > 0 && (
        <Card className="toolbarCard">
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
            <strong>{t('assets.bulkSelectedCount', { count: selectedIds.size })}</strong>
            <Button variant="secondary" onClick={() => setBulkModal('status')}>{t('assets.bulkChangeStatus')}</Button>
            <Button variant="secondary" onClick={() => setBulkModal('location')}>{t('assets.bulkMove')}</Button>
            <Button variant="secondary" onClick={exportSelectedCsv} icon={<FileSpreadsheet size={16} />}>{t('assets.bulkExport')}</Button>
            <Button variant="secondary" disabled={batchQrLoading} onClick={openBatchQr} icon={<Printer size={16} />}>{batchQrLoading ? t('common.loading') : t('assets.bulkPrintQr')}</Button>
            <Button variant="ghost" onClick={() => setSelectedIds(new Set())} icon={<X size={16} />}>{t('assets.bulkClear')}</Button>
          </div>
        </Card>
      )}

      {(owner === 'none' || warranty === 'expiring') && (
        <Card className="toolbarCard">
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap' }}>
            {owner === 'none' && <Button variant="secondary" icon={<X size={14} />} onClick={() => setOwner('')}>{t('assets.filterOwnerNone')}</Button>}
            {warranty === 'expiring' && <Button variant="secondary" icon={<X size={14} />} onClick={() => setWarranty('')}>{t('assets.filterWarrantyExpiring')}</Button>}
          </div>
        </Card>
      )}

      <Card className="toolbarCard">
        <form className="filters filters--four" onSubmit={event => event.preventDefault()}>
          <Field label={t('assets.searchLabel')}>
            <TextInput value={search} onChange={event => setSearch(event.target.value)} placeholder={t('assets.searchPlaceholder')} />
          </Field>
          <Field label={t('assets.statusLabel')}>
            <SelectInput value={status} onChange={event => setStatus(event.target.value as AssetStatus | '')}>
              {statuses.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
            </SelectInput>
          </Field>
          <Field label={t('assets.locationLabel')}>
            <SelectInput value={location} onChange={event => setLocation(event.target.value)}>
              <option value="">{t('assets.allLocations')}</option>
              {locations.data?.map(item => <option key={item.id} value={item.fullPath}>{item.fullPath}</option>)}
            </SelectInput>
          </Field>
          <Field label={t('assets.teamLabel')}>
            <SelectInput value={team} onChange={event => setTeam(event.target.value)}>
              <option value="">{t('assets.allTeams')}</option>
              {teams.data?.map(item => <option key={item.id} value={item.id}>{item.name}</option>)}
            </SelectInput>
          </Field>
        </form>
      </Card>

      <Card className="toolbarCard">
        <div className="tabs" role="tablist" aria-label={t('assets.listTitle')}>
          <button type="button" role="tab" aria-selected={viewMode === 'list'} className={viewMode === 'list' ? 'tab tab--active' : 'tab'} onClick={() => setViewMode('list')}>
            <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}><List size={16} />{t('assets.viewList')}</span>
          </button>
          <button type="button" role="tab" aria-selected={viewMode === 'location'} className={viewMode === 'location' ? 'tab tab--active' : 'tab'} onClick={() => setViewMode('location')}>
            <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}><Building2 size={16} />{t('assets.browseByLocation')}</span>
          </button>
          <button type="button" role="tab" aria-selected={viewMode === 'person'} className={viewMode === 'person' ? 'tab tab--active' : 'tab'} onClick={() => setViewMode('person')}>
            <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}><Users size={16} />{t('assets.viewPerson')}</span>
          </button>
          <button type="button" role="tab" aria-selected={viewMode === 'status'} className={viewMode === 'status' ? 'tab tab--active' : 'tab'} onClick={() => setViewMode('status')}>
            <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}><CircleDot size={16} />{t('assets.viewStatus')}</span>
          </button>
          <button type="button" role="tab" aria-selected={viewMode === 'category'} className={viewMode === 'category' ? 'tab tab--active' : 'tab'} onClick={() => setViewMode('category')}>
            <span style={{ display: 'flex', alignItems: 'center', gap: '6px' }}><Tag size={16} />{t('assets.viewCategory')}</span>
          </button>
        </div>
      </Card>

      <div style={{ display: viewMode === 'location' ? undefined : 'none' }}>
        <Card>
          <div className="sectionTitle">
            <div><h2>{t('assets.locationTreeTitle')}</h2></div>
          </div>
          <LocationAssetBrowser locations={locations.data ?? []} onSelectAsset={handleSelectAssetFromTree} />
        </Card>
      </div>

      <div style={{ display: viewMode === 'person' ? undefined : 'none' }}>
        <Card>
          <div className="sectionTitle">
            <div><h2>{t('assets.viewPerson')}</h2></div>
          </div>
          <GroupedAssetBrowser groups={personGroups} icon={<Users size={16} />} fetchGroupAssets={fetchPersonAssets} onSelectAsset={handleSelectAssetFromTree} />
        </Card>
      </div>

      <div style={{ display: viewMode === 'status' ? undefined : 'none' }}>
        <Card>
          <div className="sectionTitle">
            <div><h2>{t('assets.viewStatus')}</h2></div>
          </div>
          <GroupedAssetBrowser groups={statusGroups} icon={<CircleDot size={16} />} fetchGroupAssets={fetchStatusAssets} onSelectAsset={handleSelectAssetFromTree} />
        </Card>
      </div>

      <div style={{ display: viewMode === 'category' ? undefined : 'none' }}>
        <Card>
          <div className="sectionTitle">
            <div><h2>{t('assets.viewCategory')}</h2></div>
          </div>
          <GroupedAssetBrowser groups={categoryGroups} icon={<Tag size={16} />} fetchGroupAssets={fetchCategoryAssets} onSelectAsset={handleSelectAssetFromTree} />
        </Card>
      </div>

      <div style={{ display: viewMode === 'list' ? undefined : 'none' }}>
        <Card>
          <div className="sectionTitle">
            <div>
              <h2>{t('assets.listTitle')}</h2>
              <p>{t('assets.countSummary', { count: totalAssets, pageSize, noun: tPlural('count.assets', totalAssets) })}</p>
          </div>
        </div>

        {assets.isLoading && <p className="muted">{t('assets.refreshing')}</p>}

        {!rows.length ? (
          <EmptyState
            title={t('assets.emptyTitle')}
            description={t('assets.emptyDesc')}
            action={(debouncedSearch || status || location || team || owner || warranty) ? (
              <Button variant="secondary" icon={<X size={16} />} onClick={() => { setSearch(''); setStatus(''); setLocation(''); setTeam(''); setOwner(''); setWarranty(''); }}>{t('common.clearFilters')}</Button>
            ) : (
              <Button onClick={openCreate} icon={<Plus size={16} />}>{t('assets.add')}</Button>
            )}
          />
        ) : (
          <>
            <div className="tableWrap tableWrap--cards">
              <table className="dense-table">
                <thead>
                  <tr>
                    <th style={{ width: '32px' }}><input type="checkbox" checked={allOnPageSelected} onChange={toggleSelectAllOnPage} onClick={event => event.stopPropagation()} aria-label={t('assets.bulkSelectAll')} /></th>
                    <th style={{ width: '40px' }}></th>
                    <SortableTh sortKey="name">{t('assets.colName')}</SortableTh>
                    <SortableTh sortKey="assetTag">{t('assets.colTag')}</SortableTh>
                    <SortableTh sortKey="status">{t('assets.statusLabel')}</SortableTh>
                    <SortableTh sortKey="person">{t('assets.colPerson')}</SortableTh>
                    <SortableTh sortKey="location">{t('assets.colLocation')}</SortableTh>
                    <SortableTh sortKey="value" align="right">{t('assets.colValue')}</SortableTh>
                    <SortableTh sortKey="warranty">{t('assets.colWarranty')}</SortableTh>
                  </tr>
                </thead>
                <tbody>
                  {rows.map(asset => {
                    const cat = categories.data?.find(c => c.id === asset.categoryId);
                    return (
                      <tr
                        key={asset.id}
                        tabIndex={0}
                        onClick={() => setSelected(asset)}
                        onKeyDown={event => {
                          if (event.target !== event.currentTarget) return;
                          if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); setSelected(asset); }
                        }}
                      >
                        <td onClick={event => event.stopPropagation()}>
                          <input type="checkbox" checked={selectedIds.has(asset.id)} onChange={() => toggleSelected(asset.id)} aria-label={t('assets.bulkSelectOne', { name: asset.name })} />
                        </td>
                        <td className="cell-icon">
                          <div className="table-icon">
                            <CategoryIcon icon={cat?.icon} size={16} />
                          </div>
                        </td>
                        <td data-label={t('assets.colName')}>
                          <strong>{asset.name}</strong>
                          <small>{asset.categoryName}</small>
                        </td>
                        <td data-label={t('assets.colTag')}>{asset.assetTag}</td>
                        <td data-label={t('assets.statusLabel')}><StatusBadge status={asset.status} label={statusSettingByKey.get(asset.status)?.label} color={statusSettingByKey.get(asset.status)?.color} backgroundColor={statusSettingByKey.get(asset.status)?.backgroundColor} /></td>
                        <td data-label={t('assets.colPerson')} onClick={event => event.stopPropagation()}>
                          {asset.assignedPersonId && asset.assignedPersonName ? (
                            <button type="button" className="inlineAction" onClick={() => setViewPersonId(asset.assignedPersonId ?? null)}>{asset.assignedPersonName}</button>
                          ) : <span style={{ color: 'var(--muted)' }}>{t('common.unassigned')}</span>}
                        </td>
                        <td data-label={t('assets.colLocation')} onClick={event => event.stopPropagation()}>
                          {asset.location ? (
                            <button type="button" className="inlineAction" onClick={() => setViewLocation(asset.location ?? null)}>{asset.location}</button>
                          ) : <span style={{ color: 'var(--muted)' }}>—</span>}
                        </td>
                        <td data-label={t('assets.colValue')} style={{ textAlign: 'right' }}>{asset.purchasePrice != null ? formatMoney(asset.purchasePrice, asset.currency ?? 'PLN') : '—'}</td>
                        <td data-label={t('assets.colWarranty')}>{formatDate(asset.warrantyUntil)}</td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
            <Pagination page={page} total={totalAssets} pageSize={pageSize} onPageChange={setPage} />
          </>
        )}
        </Card>
      </div>

      <SlidePanel open={!!selected} title={selected?.name ?? t('assets.colName')} onClose={() => setSelected(null)} width="wide">
        {selected && (
          <div className="modalDetails">
            <div className="modalToolbar">
              <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                <div className="table-icon" style={{ width: '48px', height: '48px' }}>
                  <CategoryIcon icon={category?.icon} size={24} />
                </div>
                <div>
                  <strong>{selected.assetTag}</strong>
                  <small>{selected.categoryName ?? t('assets.noCategory')}</small>
                </div>
              </div>
              <div className="rowActions">
                <Button variant="secondary" onClick={() => openQr(selected)} icon={<QrCode size={16} />}>
                  {t('assets.qrCode')}
                </Button>
                <Button variant="secondary" onClick={() => openEdit(selected)} icon={<Pencil size={16} />}>
                  {t('assets.edit')}
                </Button>
                <Button
                  variant="secondary"
                  onClick={() => {
                    setDeleteTarget(selected);
                    setSelected(null);
                  }}
                  icon={<Trash2 size={16} />}
                >
                  {t('assets.delete')}
                </Button>
              </div>
            </div>

            <dl className="detailGrid">
              <div>
                <dt>{t('assets.statusLabel')}</dt>
                <dd><StatusBadge status={selected.status} label={statusSettingByKey.get(selected.status)?.label} color={statusSettingByKey.get(selected.status)?.color} backgroundColor={statusSettingByKey.get(selected.status)?.backgroundColor} /></dd>
              </div>
              <div>
                <dt>{t('assets.colPerson')}</dt>
                <dd>
                  {selected.assignedPersonId && selected.assignedPersonName ? (
                    <button type="button" className="inlineAction" onClick={() => setViewPersonId(selected.assignedPersonId ?? null)}>{selected.assignedPersonName}</button>
                  ) : t('common.unassigned')}
                </dd>
              </div>
              <div>
                <dt>{t('assets.colLocation')}</dt>
                <dd>
                  {selected.location ? (
                    <button type="button" className="inlineAction" onClick={() => setViewLocation(selected.location ?? null)}>{selected.location}</button>
                  ) : t('common.noLocation')}
                </dd>
              </div>
              <div>
                <dt>{t('assets.teamLabel')}</dt>
                <dd>{selected.teamName ?? t('common.none')}</dd>
              </div>
              <div>
                <dt>{t('assets.serialNumber')}</dt>
                <dd>{selected.serialNumber ?? t('common.none')}</dd>
              </div>
              <div>
                <dt>{t('assets.manufacturerModel')}</dt>
                <dd>{[selected.manufacturer, selected.model].filter(Boolean).join(' ') || t('common.none')}</dd>
              </div>
              <div>
                <dt>{t('assets.purchase')}</dt>
                <dd>
                  {selected.purchasePrice != null
                    ? `${formatMoney(selected.purchasePrice, selected.currency ?? 'PLN')} · ${formatDate(selected.purchaseDate)}`
                    : t('assets.noPurchaseData')}
                </dd>
              </div>
              <div>
                <dt>{t('assets.warranty')}</dt>
                <dd>{formatDate(selected.warrantyUntil)}</dd>
              </div>
              {selected.categoryFieldDefinitions.map(field => (
                <div key={field.id}>
                  <dt>{field.label}</dt>
                  <dd>
                    {field.fieldType === 'Boolean' ? (
                      selected.customFields[field.key] === 'true' ? t('common.yes') : t('common.no')
                    ) : field.fieldType === 'Sensitive' ? (
                      <span style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <span>{revealedFields[field.key] ?? selected.customFields[field.key] ?? t('common.none')}</span>
                        {revealedFields[field.key] === undefined && selected.customFields[field.key] && (
                          <button type="button" className="iconButton" aria-label={t('assets.revealField')} title={t('assets.revealField')} disabled={revealingKey === field.key} onClick={() => revealField(field.key)}>
                            <Eye size={14} />
                          </button>
                        )}
                      </span>
                    ) : (
                      selected.customFields[field.key] ?? t('common.none')
                    )}
                  </dd>
                </div>
              ))}
            </dl>

            <div className="formSectionTitle">{t('evidence.photos')}</div>
            {evidence.isLoading ? <p className="muted">{t('common.loading')}</p> : !evidence.data?.length ? (
              <p className="muted">{t('evidence.noPhotos')}</p>
            ) : (
              <div className="pageStack">
                {(['Issue', 'Return', 'Audit', 'Offboarding'] as const).map(phase => {
                  const ids = evidence.data!.filter(item => item.phase === phase).map(item => item.id);
                  if (!ids.length) return null;
                  return (
                    <div key={phase}>
                      <strong>{t(`evidence.phase.${phase}`)}</strong>
                      <EvidenceGallery ids={ids} getBlob={api.evidenceBlob} />
                    </div>
                  );
                })}
              </div>
            )}

            <div className="formSectionTitle">{t('serviceTickets.title')}</div>
            <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '8px' }}>
              <Button
                variant="secondary"
                onClick={() => setServiceTicketModalOpen(true)}
                icon={<Plus size={16} />}
                disabled={selected.status === 'Disposed'}
                title={selected.status === 'Disposed' ? t('serviceTickets.cannotOpenForDisposed') : undefined}
              >
                {t('serviceTickets.open')}
              </Button>
            </div>
            {serviceTickets.isLoading ? <p className="muted">{t('common.loading')}</p> : !serviceTickets.data?.length ? (
              <p className="muted">{t('serviceTickets.none')}</p>
            ) : (
              <div className="listRows">
                {serviceTickets.data.map(ticket => {
                  const cost = ticket.actualCost ?? ticket.estimatedCost;
                  const isOpen = ticket.status === 'Open' || ticket.status === 'InProgress' || ticket.status === 'WaitingForParts';
                  return (
                    <div className="listRow" key={ticket.id}>
                      <div>
                        <strong>{ticket.vendor}</strong>
                        <small>
                          <span className={ticketStatusClass(ticket.status)}>{serviceTicketStatusLabels[ticket.status]}</span>
                          {' · '}{formatDate(ticket.openedAt)}
                          {cost != null && ` · ${formatMoney(cost, ticket.currency ?? 'PLN')}`}
                        </small>
                      </div>
                      {isOpen ? (
                        <div style={{ display: 'flex', gap: '8px' }}>
                          <Button variant="secondary" onClick={() => setCompletingTicket(ticket)}>{t('serviceTickets.complete')}</Button>
                          <Button variant="ghost" onClick={() => setCancellingTicket(ticket)}>{t('serviceTickets.cancel')}</Button>
                        </div>
                      ) : null}
                    </div>
                  );
                })}
              </div>
            )}

            <div className="formSectionTitle">{t('assets.historyTitle')}</div>
            {history.isLoading ? <p className="muted">{t('common.loading')}</p> : !history.data?.items.length ? (
              <p className="muted">{t('assets.noHistory')}</p>
            ) : (
              <div className="listRows">
                {history.data.items.map(entry => (
                  <div className="listRow" key={entry.id}>
                    <div><strong>{activityLabel(t, entry.action)}</strong><small>{entry.actorDisplay}</small></div>
                    <small>{formatDateTime(entry.createdAt)}</small>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}
      </SlidePanel>

      <Modal
        open={assetModalOpen}
        title={editing ? t('assets.editTitle') : t('assets.addTitle')}
        onClose={closeAssetModal}
        width="wide"
      >
        <form className="formGrid" onSubmit={handleSave} key={editing?.id ?? 'new-asset'}>
          <div className="formSectionTitle">{t('assets.identification')}</div>
          <Field label={t('assets.nameLabel')}><TextInput name="name" defaultValue={editing?.name ?? ''} required /></Field>
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
            <SelectInput name="teamId" defaultValue={editing?.teamId ?? ''}>
              <option value="">{t('assets.noTeamOption')}</option>
              {teams.data?.map(team => <option key={team.id} value={team.id}>{team.name}</option>)}
            </SelectInput>
          </Field>

          <div className="formSectionTitle">{t('assets.descAndDates')}</div>
          <Field label={t('assets.manufacturerLabel')}><TextInput name="manufacturer" defaultValue={editing?.manufacturer ?? ''} /></Field>
          <Field label={t('assets.modelLabel')}><TextInput name="model" defaultValue={editing?.model ?? ''} /></Field>
          <Field label={t('assets.purchasePriceLabel')}><TextInput name="purchasePrice" inputMode="decimal" defaultValue={editing?.purchasePrice ?? ''} /></Field>
          <Field label={t('assets.currencyLabel')}><TextInput name="currency" defaultValue={editing?.currency ?? 'PLN'} maxLength={3} /></Field>
          <Field label={t('assets.purchaseDateLabel')}><TextInput name="purchaseDate" type="date" defaultValue={editing?.purchaseDate ?? ''} /></Field>
          <Field label={t('assets.warrantyUntilLabel')}><TextInput name="warrantyUntil" type="date" defaultValue={editing?.warrantyUntil ?? ''} /></Field>

          {selectedCategoryFields.length > 0 && (
            <>
              <div className="formSectionTitle">{t('assets.customFieldsSection')}</div>
              {selectedCategoryFields.map(field => (
                <Field key={field.id} label={field.required ? `${field.label} *` : field.label}>
                  {field.fieldType === 'Boolean' ? (
                    <label style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <input type="checkbox" name={`custom__${field.key}`} defaultChecked={editing?.customFields?.[field.key] === 'true'} />
                    </label>
                  ) : field.fieldType === 'Select' ? (
                    <SelectInput name={`custom__${field.key}`} defaultValue={editing?.customFields?.[field.key] ?? ''} required={field.required}>
                      <option value="">{t('assets.customFieldChoose')}</option>
                      {field.options.map(option => <option key={option} value={option}>{option}</option>)}
                    </SelectInput>
                  ) : (
                    <TextInput
                      name={`custom__${field.key}`}
                      type={field.fieldType === 'Number' ? 'number' : field.fieldType === 'Date' ? 'date' : field.fieldType === 'Sensitive' ? 'password' : 'text'}
                      defaultValue={field.fieldType === 'Sensitive' ? '' : editing?.customFields?.[field.key] ?? ''}
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

      <Modal open={!!qrTarget} title={qrTarget ? `${t('assets.qrCode')} — ${qrTarget.assetTag}` : t('assets.qrCode')} onClose={() => setQrTarget(null)}>
        {qrLoading ? <p className="muted">{t('assets.generatingQr')}</p> : qrSvg ? (
          <div className="qrPreview">
            <div className="qrPreview__image" dangerouslySetInnerHTML={{ __html: qrSvg }} />
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
        onClose={() => setImportOpen(false)}
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

      <Modal open={!!batchQr} title={t('assets.bulkPrintQrTitle')} onClose={() => setBatchQr(null)} width="wide">
        {batchQr && (
          <>
            <div className="qrPrintSheet">
              {batchQr.map(item => (
                <div className="qrPrintCard" key={item.asset.id}>
                  <div dangerouslySetInnerHTML={{ __html: item.svg }} />
                </div>
              ))}
            </div>
            <div className="formActions formActions--split">
              <Button type="button" variant="ghost" onClick={() => setBatchQr(null)}>{t('common.close')}</Button>
              <Button type="button" onClick={() => window.print()} icon={<Printer size={16} />}>{t('assets.bulkPrintQr')}</Button>
            </div>
          </>
        )}
      </Modal>
    </div>
  );
}
