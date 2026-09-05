export function statusBadgeClass(
  status: string
): string {

  switch (status) {

    case 'New':
      return 'bg-blue-50 text-blue-700 ring-blue-600/20';

    case 'Confirmed':
      return 'bg-indigo-50 text-indigo-700 ring-indigo-600/20';

    case 'In Production':
      return 'bg-amber-50 text-amber-700 ring-amber-600/20';

    case 'Quality Check':
      return 'bg-purple-50 text-purple-700 ring-purple-600/20';

    case 'Shipped':
      return 'bg-cyan-50 text-cyan-700 ring-cyan-600/20';

    case 'Delivered':
      return 'bg-green-50 text-green-700 ring-green-600/20';

    default:
      return 'bg-gray-100 text-gray-600 ring-gray-500/20';
  }
}

export function priorityBadgeClass(
  priority: string | null
): string {

  if (!priority) {
    return '';
  }

  if (
    priority
      .toLowerCase()
      .includes('urgent')
  ) {
    return 'bg-red-50 text-red-700 ring-red-600/20';
  }

  return 'bg-orange-50 text-orange-700 ring-orange-600/20';
}