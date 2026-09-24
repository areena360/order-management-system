export function statusBadgeClass(
  status: string
): string {

  switch (status) {

    case 'Assign':
      return 'bg-blue-50 text-blue-700 ring-blue-600/20';

    case 'In Manufacturing':
      return 'bg-amber-50 text-amber-700 ring-amber-600/20';

    case 'Refund':
      return 'bg-purple-50 text-purple-700 ring-purple-600/20';

    case 'Cancel':
      return 'bg-red-50 text-red-700 ring-red-600/20';

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
