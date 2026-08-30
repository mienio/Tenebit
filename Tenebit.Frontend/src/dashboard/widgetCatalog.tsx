import { BarChart3, Boxes, CalendarClock, CheckCircle2, ClipboardList, History, KeyRound, PackageOpen, PieChart, ShieldCheck, Users, Wrench } from 'lucide-react';
import type { ReactNode } from 'react';
import type { Layout } from 'react-grid-layout';

export type WidgetType =
  | 'metric-assets'
  | 'metric-inStock'
  | 'metric-assigned'
  | 'metric-people'
  | 'metric-openAssignments'
  | 'metric-value'
  | 'metric-licenses'
  | 'chart-byStatus'
  | 'chart-byCategory'
  | 'list-warranty'
  | 'list-maintenance'
  | 'list-activity';

export interface WidgetDef {
  type: WidgetType;
  titleKey: string;
  defaultSize: { w: number; h: number };
  minW: number;
  minH: number;
}

export const GRID_COLS = 20;

export const WIDGET_CATALOG: WidgetDef[] = [
  { type: 'metric-assets', titleKey: 'metric.assets', defaultSize: { w: 4, h: 3 }, minW: 3, minH: 3 },
  { type: 'metric-inStock', titleKey: 'metric.inStock', defaultSize: { w: 4, h: 3 }, minW: 3, minH: 3 },
  { type: 'metric-assigned', titleKey: 'metric.assigned', defaultSize: { w: 4, h: 3 }, minW: 3, minH: 3 },
  { type: 'metric-people', titleKey: 'metric.people', defaultSize: { w: 4, h: 3 }, minW: 3, minH: 3 },
  { type: 'metric-openAssignments', titleKey: 'metric.openAssignments', defaultSize: { w: 4, h: 3 }, minW: 3, minH: 3 },
  { type: 'metric-value', titleKey: 'metric.visibleValue', defaultSize: { w: 4, h: 3 }, minW: 3, minH: 3 },
  { type: 'metric-licenses', titleKey: 'metric.licenses', defaultSize: { w: 4, h: 3 }, minW: 3, minH: 3 },
  { type: 'chart-byStatus', titleKey: 'dashboard.byStatus', defaultSize: { w: 10, h: 5 }, minW: 6, minH: 4 },
  { type: 'chart-byCategory', titleKey: 'dashboard.byCategory', defaultSize: { w: 10, h: 5 }, minW: 6, minH: 4 },
  { type: 'list-warranty', titleKey: 'dashboard.warrantyDeadlines', defaultSize: { w: 10, h: 5 }, minW: 6, minH: 4 },
  { type: 'list-maintenance', titleKey: 'maintenance.dueTitle', defaultSize: { w: 10, h: 5 }, minW: 6, minH: 4 },
  { type: 'list-activity', titleKey: 'dashboard.recentActivity', defaultSize: { w: 20, h: 6 }, minW: 10, minH: 4 }
];

export const WIDGET_CATALOG_MAP: Record<WidgetType, WidgetDef> = Object.fromEntries(WIDGET_CATALOG.map(widget => [widget.type, widget])) as Record<WidgetType, WidgetDef>;

export const WIDGET_ICONS: Record<WidgetType, ReactNode> = {
  'metric-assets': <Boxes size={18} />,
  'metric-inStock': <PackageOpen size={18} />,
  'metric-assigned': <CheckCircle2 size={18} />,
  'metric-people': <Users size={18} />,
  'metric-openAssignments': <ClipboardList size={18} />,
  'metric-value': <ShieldCheck size={18} />,
  'metric-licenses': <KeyRound size={18} />,
  'chart-byStatus': <PieChart size={18} />,
  'chart-byCategory': <BarChart3 size={18} />,
  'list-warranty': <CalendarClock size={18} />,
  'list-maintenance': <Wrench size={18} />,
  'list-activity': <History size={18} />
};

// Cztery liczniki w jednym pełnym rzędzie. Sześć kafelków ściśniętych po 3 z 20 kolumn ucinało
// etykiety i wartości; pozostałe metryki nadal można dołożyć z listy widgetów.
const DEFAULT_METRICS: WidgetType[] = [
  'metric-assets',
  'metric-inStock',
  'metric-assigned',
  'metric-openAssignments'
];

const METRICS_PER_ROW = 4;

export function buildDefaultLayout(): Layout[] {
  const layout: Layout[] = [];
  const metrics = DEFAULT_METRICS;
  const colWidth = Math.floor(GRID_COLS / METRICS_PER_ROW);
  metrics.forEach((type, index) => {
    const row = Math.floor(index / METRICS_PER_ROW);
    const col = index % METRICS_PER_ROW;
    layout.push({ i: type, x: col * colWidth, y: row * 3, w: colWidth, h: WIDGET_CATALOG_MAP[type].defaultSize.h, minW: WIDGET_CATALOG_MAP[type].minW, minH: WIDGET_CATALOG_MAP[type].minH });
  });
  const chartsY = Math.ceil(metrics.length / METRICS_PER_ROW) * 3;
  layout.push({ i: 'chart-byStatus', x: 0, y: chartsY, ...WIDGET_CATALOG_MAP['chart-byStatus'].defaultSize });
  layout.push({ i: 'chart-byCategory', x: 10, y: chartsY, ...WIDGET_CATALOG_MAP['chart-byCategory'].defaultSize });
  layout.push({ i: 'list-activity', x: 0, y: chartsY + 5, ...WIDGET_CATALOG_MAP['list-activity'].defaultSize });
  return layout;
}
