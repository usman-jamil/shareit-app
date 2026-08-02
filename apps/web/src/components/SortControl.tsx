import MenuItem from '@mui/material/MenuItem'
import Stack from '@mui/material/Stack'
import TextField from '@mui/material/TextField'
import Tooltip from '@mui/material/Tooltip'
import IconButton from '@mui/material/IconButton'
import { SortAscending, SortDescending } from '@phosphor-icons/react'
import type { SortDir, SortKey } from '@/types'

const OPTIONS: { value: SortKey; label: string }[] = [
  { value: 'name', label: 'Name' },
  { value: 'modified', label: 'Date modified' },
  { value: 'type', label: 'Type' },
]

export interface SortControlProps {
  sortKey: SortKey
  sortDir: SortDir
  onSortKeyChange: (key: SortKey) => void
  onSortDirToggle: () => void
}

export function SortControl({
  sortKey,
  sortDir,
  onSortKeyChange,
  onSortDirToggle,
}: SortControlProps) {
  return (
    <Stack direction="row" gap={1}>
      <TextField
        select
        size="small"
        value={sortKey}
        onChange={(e) => onSortKeyChange(e.target.value as SortKey)}
        inputProps={{ 'aria-label': 'Sort files by' }}
        sx={{
          minWidth: 168,
          '& .MuiOutlinedInput-root': { bgcolor: 'background.paper' },
        }}
      >
        {OPTIONS.map((o) => (
          <MenuItem key={o.value} value={o.value}>
            Sort: {o.label}
          </MenuItem>
        ))}
      </TextField>
      <Tooltip title={sortDir === 'asc' ? 'Ascending' : 'Descending'}>
        <IconButton
          onClick={onSortDirToggle}
          aria-label="Reverse sort order"
          sx={{
            border: 1,
            borderColor: 'divider',
            borderRadius: 1,
            bgcolor: 'background.paper',
            width: 40,
            height: 40,
          }}
        >
          {sortDir === 'asc' ? (
            <SortAscending size={17} weight="duotone" />
          ) : (
            <SortDescending size={17} weight="duotone" />
          )}
        </IconButton>
      </Tooltip>
    </Stack>
  )
}
