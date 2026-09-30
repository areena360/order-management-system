export function manufacturingStatusLabel(status: string | null | undefined): string {
  return ({ assigned: 'Assigned', inprogress: 'In Progress', done: 'Done', unassigned: 'Removed' } as Record<string, string>)[status ?? ''] ?? 'Assigned';
}

export function manufacturingStatusClass(status: string | null | undefined): string {
  switch (status) {
    case 'done': return 'bg-emerald-50 text-emerald-700 ring-emerald-200';
    case 'inprogress': return 'bg-amber-50 text-amber-700 ring-amber-200';
    case 'unassigned': return 'bg-gray-100 text-gray-500 ring-gray-200';
    default: return 'bg-blue-50 text-blue-700 ring-blue-200';
  }
}
