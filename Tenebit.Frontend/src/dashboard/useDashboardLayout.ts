import type { Layout } from 'react-grid-layout';
import { useCallback, useEffect, useState } from 'react';
import { api } from '../api/endpoints';
import { WIDGET_CATALOG, WIDGET_CATALOG_MAP, buildDefaultLayout, type WidgetType } from './widgetCatalog';

function withCatalogSizing(layout: Layout[]): Layout[] {
  return layout
    .filter(item => item.i in WIDGET_CATALOG_MAP)
    .map(item => {
      const def = WIDGET_CATALOG_MAP[item.i as WidgetType];
      return { ...item, minW: def.minW, minH: def.minH };
    });
}

// Widgets added to the default set after users could already have a saved
// layout: backfill them once so they don't stay permanently invisible.
// Deliberately NOT re-added if a user removes them afterwards.
const LEGACY_BACKFILL_WIDGETS: WidgetType[] = ['metric-licenses'];

function withLegacyBackfill(layout: Layout[]): Layout[] {
  const missing = LEGACY_BACKFILL_WIDGETS.filter(type => !layout.some(item => item.i === type));
  if (missing.length === 0) return layout;
  let maxY = layout.reduce((max, item) => Math.max(max, item.y + item.h), 0);
  const added = missing.map(type => {
    const def = WIDGET_CATALOG_MAP[type];
    const item = { i: type, x: 0, y: maxY, ...def.defaultSize, minW: def.minW, minH: def.minH };
    maxY += def.defaultSize.h;
    return item;
  });
  return [...layout, ...added];
}

export function useDashboardLayout() {
  const [widgets, setWidgets] = useState<Layout[] | null>(null);
  const [editing, setEditing] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState(false);
  const [savedSnapshot, setSavedSnapshot] = useState<Layout[] | null>(null);

  useEffect(() => {
    let cancelled = false;
    api.dashboardLayout()
      .then(res => {
        if (cancelled) return;
        if (!res.layoutJson) {
          setWidgets(buildDefaultLayout());
          return;
        }
        try {
          const parsed = JSON.parse(res.layoutJson) as Layout[];
          const cleaned = withCatalogSizing(parsed);
          setWidgets(cleaned.length > 0 ? withLegacyBackfill(cleaned) : buildDefaultLayout());
        } catch {
          setWidgets(buildDefaultLayout());
        }
      })
      .catch(() => setWidgets(buildDefaultLayout()));
    return () => { cancelled = true; };
  }, []);

  const startEdit = useCallback(() => {
    setSavedSnapshot(widgets);
    setSaveError(false);
    setEditing(true);
  }, [widgets]);

  const cancelEdit = useCallback(() => {
    setWidgets(savedSnapshot);
    setSaveError(false);
    setEditing(false);
  }, [savedSnapshot]);

  const finishEdit = useCallback(async () => {
    if (!widgets) return;
    setSaving(true);
    setSaveError(false);
    try {
      const payload = widgets.map(({ i, x, y, w, h }) => ({ i, x, y, w, h }));
      await api.saveDashboardLayout(JSON.stringify(payload));
      setEditing(false);
    } catch {
      setSaveError(true);
    } finally {
      setSaving(false);
    }
  }, [widgets]);

  const addWidget = useCallback((type: WidgetType) => {
    setWidgets(current => {
      const list = current ?? [];
      if (list.some(item => item.i === type)) return list;
      const def = WIDGET_CATALOG_MAP[type];
      const maxY = list.reduce((max, item) => Math.max(max, item.y + item.h), 0);
      return [...list, { i: type, x: 0, y: maxY, w: def.defaultSize.w, h: def.defaultSize.h, minW: def.minW, minH: def.minH }];
    });
  }, []);

  const removeWidget = useCallback((type: WidgetType) => {
    setWidgets(current => (current ?? []).filter(item => item.i !== type));
  }, []);

  const availableToAdd = widgets ? WIDGET_CATALOG.filter(def => !widgets.some(item => item.i === def.type)) : [];

  return { widgets, setWidgets, editing, saving, saveError, startEdit, cancelEdit, finishEdit, addWidget, removeWidget, availableToAdd };
}
