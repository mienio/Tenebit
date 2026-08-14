import type { AssetStatus } from '../types/domain';

const statusColorMap: Record<AssetStatus, string> = {
  Draft: '#8a8074',
  InStock: '#5f7a5f',
  Reserved: '#5b7c99',
  Assigned: '#a63a2e',
  PendingReturn: '#b45309',
  InTransit: '#c08a1f',
  InService: '#7a4a5c',
  Damaged: '#b91c1c',
  Lost: '#7c2d12',
  Retired: '#6b6259',
  Disposed: '#4a4038'
};

export function statusColor(status: AssetStatus): string {
  return statusColorMap[status] ?? '#8a8074';
}
