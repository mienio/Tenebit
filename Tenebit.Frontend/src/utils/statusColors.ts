import type { AssetStatus } from '../types/domain';

const statusColorMap: Record<AssetStatus, string> = {
  Draft: '#475569',
  InStock: '#047857',
  Reserved: '#1d4ed8',
  Assigned: '#1d4ed8',
  InTransit: '#c2410c',
  InService: '#c2410c',
  Damaged: '#be123c',
  Lost: '#be123c',
  Retired: '#475569',
  Disposed: '#991b1b'
};

export function statusColor(status: AssetStatus): string {
  return statusColorMap[status] ?? '#475569';
}
