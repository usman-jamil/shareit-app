import Stack from '@mui/material/Stack'
import type { Crumb, SortDir, SortKey, ViewMode } from '@/types'
import { FilterField } from './FilterField'
import { ShareBreadcrumbs } from './ShareBreadcrumbs'
import { SortControl } from './SortControl'
import { ViewModeToggle } from './ViewModeToggle'

export interface ShareToolbarProps {
  crumbs: Crumb[]
  query: string
  sortKey: SortKey
  sortDir: SortDir
  view: ViewMode
  onNavigate: (path: string[]) => void
  onQueryChange: (value: string) => void
  onSortKeyChange: (key: SortKey) => void
  onSortDirToggle: () => void
  onViewChange: (mode: ViewMode) => void
}

/** Composition only — every control below is independently usable. */
export function ShareToolbar({
  crumbs,
  query,
  sortKey,
  sortDir,
  view,
  onNavigate,
  onQueryChange,
  onSortKeyChange,
  onSortDirToggle,
  onViewChange,
}: ShareToolbarProps) {
  return (
    <Stack
      direction="row"
      flexWrap="wrap"
      alignItems="center"
      justifyContent="space-between"
      gap={2}
      sx={{ mb: 0.75 }}
    >
      <ShareBreadcrumbs crumbs={crumbs} onNavigate={onNavigate} />
      <Stack direction="row" flexWrap="wrap" alignItems="center" gap={1.25}>
        <FilterField value={query} onChange={onQueryChange} />
        <SortControl
          sortKey={sortKey}
          sortDir={sortDir}
          onSortKeyChange={onSortKeyChange}
          onSortDirToggle={onSortDirToggle}
        />
        <ViewModeToggle value={view} onChange={onViewChange} />
      </Stack>
    </Stack>
  )
}
