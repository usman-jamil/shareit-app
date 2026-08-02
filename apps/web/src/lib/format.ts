const MONTHS = [
  'Jan',
  'Feb',
  'Mar',
  'Apr',
  'May',
  'Jun',
  'Jul',
  'Aug',
  'Sep',
  'Oct',
  'Nov',
  'Dec',
]

export function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`
  if (bytes < 1024 * 1024)
    return `${(bytes / 1024).toFixed(bytes < 10_240 ? 1 : 0)} KB`
  return `${(bytes / 1_048_576).toFixed(1)} MB`
}

/** "30 Jul 2026, 14:22" — parsed literally, so no timezone drift on a fixture. */
export function formatDate(iso: string | undefined): string {
  if (!iso) return '—'
  const [datePart, timePart = ''] = iso.split('T')
  const [year, month, day] = (datePart ?? '').split('-')
  if (!year || !month || !day) return '—'
  const monthName = MONTHS[Number(month) - 1] ?? ''
  return `${Number(day)} ${monthName} ${year}, ${timePart.slice(0, 5)}`
}

export function formatCount(n: number, noun: string): string {
  return `${n} ${noun}${n === 1 ? '' : 's'}`
}

export function formatExpiry(minutes: number): string {
  if (minutes <= 0) return 'Expired'
  if (minutes < 60) return `Expires in ${formatCount(minutes, 'minute')}`
  const hours = Math.floor(minutes / 60)
  const rest = minutes % 60
  return rest
    ? `Expires in ${formatCount(hours, 'hour')} ${formatCount(rest, 'minute')}`
    : `Expires in ${formatCount(hours, 'hour')}`
}

export function extensionOf(name: string): string {
  const i = name.lastIndexOf('.')
  return i > 0 ? name.slice(i + 1).toLowerCase() : ''
}

const TYPE_LABELS: Record<string, string> = {
  ts: 'TypeScript',
  tsx: 'TypeScript',
  js: 'JavaScript',
  jsx: 'JavaScript',
  json: 'JSON',
  md: 'Markdown',
  css: 'Stylesheet',
  html: 'HTML',
  svg: 'Vector',
  png: 'Image',
  jpg: 'Image',
  jpeg: 'Image',
  yml: 'YAML',
  yaml: 'YAML',
  lcov: 'Coverage',
  lock: 'Lockfile',
}

export function typeLabelFor(name: string): string {
  const ext = extensionOf(name)
  return TYPE_LABELS[ext] ?? (ext ? ext.toUpperCase() : 'File')
}
