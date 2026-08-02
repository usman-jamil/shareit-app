import type { ReactNode } from 'react'
import ToggleButton from '@mui/material/ToggleButton'
import ToggleButtonGroup from '@mui/material/ToggleButtonGroup'
import Tooltip from '@mui/material/Tooltip'
import { ListDashes, SquaresFour, TreeStructure } from '@phosphor-icons/react'
import type { ViewMode } from '@/types'

export interface ViewModeToggleProps {
  value: ViewMode
  onChange: (mode: ViewMode) => void
}

const MODES: { value: ViewMode; label: string; icon: ReactNode }[] = [
  {
    value: 'tiles',
    label: 'Tiles',
    icon: <SquaresFour size={18} weight="duotone" />,
  },
  {
    value: 'details',
    label: 'Details',
    icon: <ListDashes size={18} weight="duotone" />,
  },
  {
    value: 'tree',
    label: 'Tree',
    icon: <TreeStructure size={18} weight="duotone" />,
  },
]

export function ViewModeToggle({ value, onChange }: ViewModeToggleProps) {
  return (
    <ToggleButtonGroup
      exclusive
      size="small"
      value={value}
      onChange={(_, next: ViewMode | null) => next && onChange(next)}
      aria-label="View mode"
      sx={{ height: 40, bgcolor: 'background.paper' }}
    >
      {MODES.map((m) => (
        <Tooltip key={m.value} title={m.label}>
          <ToggleButton value={m.value} aria-label={m.label} sx={{ width: 40 }}>
            {m.icon}
          </ToggleButton>
        </Tooltip>
      ))}
    </ToggleButtonGroup>
  )
}
