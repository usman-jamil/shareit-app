import Box from '@mui/material/Box'
import ButtonBase from '@mui/material/ButtonBase'
import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { CaretDown, CaretRight, DotOutline } from '@phosphor-icons/react'
import type { RowModel } from '@/types'
import { NodeIcon } from '../NodeIcon'
import { RowActions } from '../RowActions'

export interface TreeViewProps {
  rows: RowModel[]
  /** Folders toggle open; files download. */
  onToggle: (row: RowModel) => void
  onOpen: (row: RowModel) => void
  onAction: (row: RowModel) => void
}

const INDENT_STEP = 22

export function TreeView({ rows, onToggle, onOpen, onAction }: TreeViewProps) {
  return (
    <Paper
      elevation={1}
      sx={{ mt: 2.25, py: 1.25, overflow: 'hidden' }}
      role="tree"
    >
      {rows.map((row) => (
        <Stack
          key={row.id}
          direction="row"
          alignItems="center"
          gap={1.25}
          role="treeitem"
          aria-level={row.depth + 1}
          aria-expanded={row.isFolder ? row.expanded : undefined}
          sx={{
            height: 36,
            pr: 2,
            pl: `${14 + row.depth * INDENT_STEP}px`,
            '&:hover': { bgcolor: 'action.hover' },
          }}
        >
          <ButtonBase
            onClick={() => (row.isFolder ? onToggle(row) : onOpen(row))}
            sx={{
              flex: 1,
              minWidth: 0,
              height: '100%',
              display: 'flex',
              alignItems: 'center',
              gap: 1.125,
              justifyContent: 'flex-start',
              textAlign: 'left',
              borderRadius: 1,
            }}
          >
            <Box
              component="span"
              sx={{ width: 13, display: 'flex', opacity: 0.55, flex: 'none' }}
            >
              {row.isFolder ? (
                row.expanded ? (
                  <CaretDown size={13} weight="bold" />
                ) : (
                  <CaretRight size={13} weight="bold" />
                )
              ) : (
                <DotOutline size={13} weight="fill" />
              )}
            </Box>
            <NodeIcon name={row.name} isFolder={row.isFolder} size={19} />
            <Typography
              component="span"
              sx={{
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                whiteSpace: 'nowrap',
                fontSize: '0.95rem',
              }}
            >
              {row.name}
            </Typography>
          </ButtonBase>
          <Typography
            variant="body2"
            sx={{
              color: 'text.secondary',
              fontVariantNumeric: 'tabular-nums',
              flex: 'none',
            }}
          >
            {row.metaLabel}
          </Typography>
          <RowActions row={row} size={28} onAction={onAction} />
        </Stack>
      ))}
    </Paper>
  )
}
