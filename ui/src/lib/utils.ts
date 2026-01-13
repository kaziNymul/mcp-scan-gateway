import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function formatDate(dateString: string): string {
  return new Date(dateString).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function getRiskColor(score: number): string {
  if (score <= 30) return 'text-green-600 bg-green-100';
  if (score <= 50) return 'text-yellow-600 bg-yellow-100';
  if (score <= 70) return 'text-orange-600 bg-orange-100';
  return 'text-red-600 bg-red-100';
}

export function getStatusColor(status: string): string {
  switch (status) {
    case 'Approved':
      return 'bg-green-100 text-green-800';
    case 'Denied':
    case 'Suspended':
      return 'bg-red-100 text-red-800';
    case 'Scanning':
    case 'PendingScan':
      return 'bg-blue-100 text-blue-800';
    case 'ScannedPass':
      return 'bg-emerald-100 text-emerald-800';
    case 'ScannedFail':
      return 'bg-orange-100 text-orange-800';
    case 'PendingApproval':
      return 'bg-yellow-100 text-yellow-800';
    case 'Draft':
      return 'bg-gray-100 text-gray-800';
    case 'Deprecated':
      return 'bg-purple-100 text-purple-800';
    default:
      return 'bg-gray-100 text-gray-800';
  }
}
