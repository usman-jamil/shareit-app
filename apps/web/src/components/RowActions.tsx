import IconButton from '@mui/material/IconButton'
import Tooltip from '@mui/material/Tooltip'
import { FileZip, LinkSimple } from '@phosphor-icons/react'
import type { RowModel } from '@/types'

export interface RowActionsProps {
  row: RowModel
  size?: number
  /** Zip a folder, or copy a direct link to a file. */
  onAction: (row: RowModel) => void
}

export function RowActions({ row, size = 32, onAction }: RowActionsProps) {
  const title = row.isFolder ? 'Download folder as .zip' : 'Copy direct link'
  return (
    <Tooltip title={title}>
      <IconButton
        size="small"
        aria-label={`${title}: ${row.name}`}
        onClick={(e) => {
          e.stopPropagation()
          onAction(row)
        }}
        sx={{
          width: size,
          height: size,
          color: 'text.secondary',
          '&:hover': { color: 'primary.main', bgcolor: 'action.hover' },
        }}
      >
        {row.isFolder ? (
          <FileZip size={size / 2} weight="duotone" />
        ) : (
          <LinkSimple size={size / 2} weight="duotone" />
        )}
      </IconButton>
    </Tooltip>
  )
}
