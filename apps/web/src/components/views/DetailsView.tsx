import Box from '@mui/material/Box'
import Paper from '@mui/material/Paper'
import Table from '@mui/material/Table'
import TableBody from '@mui/material/TableBody'
import TableCell from '@mui/material/TableCell'
import TableContainer from '@mui/material/TableContainer'
import TableHead from '@mui/material/TableHead'
import TableRow from '@mui/material/TableRow'
import TableSortLabel from '@mui/material/TableSortLabel'
import ButtonBase from '@mui/material/ButtonBase'
import type { RowModel, SortDir, SortKey } from '@/types'
import { NodeIcon } from '../NodeIcon'
import { RowActions } from '../RowActions'

export interface DetailsViewProps {
  rows: RowModel[]
  sortKey: SortKey
  sortDir: SortDir
  onSort: (key: SortKey) => void
  /** Open a folder, or download a file. */
  onOpen: (row: RowModel) => void
  onAction: (row: RowModel) => void
}

const HIDE_SM = { display: { xs: 'none', md: 'table-cell' } } as const
const HIDE_XS = { display: { xs: 'none', sm: 'table-cell' } } as const

export function DetailsView({
  rows,
  sortKey,
  sortDir,
  onSort,
  onOpen,
  onAction,
}: DetailsViewProps) {
  return (
    <TableContainer component={Paper} elevation={1} sx={{ mt: 2.25 }}>
      <Table size="small" aria-label="Share contents">
        <TableHead>
          <TableRow>
            <TableCell>
              <TableSortLabel
                active={sortKey === 'name'}
                direction={sortKey === 'name' ? sortDir : 'asc'}
                onClick={() => onSort('name')}
              >
                Name
              </TableSortLabel>
            </TableCell>
            <TableCell sx={HIDE_SM}>
              <TableSortLabel
                active={sortKey === 'type'}
                direction={sortKey === 'type' ? sortDir : 'asc'}
                onClick={() => onSort('type')}
              >
                Type
              </TableSortLabel>
            </TableCell>
            <TableCell align="right" sx={HIDE_SM}>
              Size
            </TableCell>
            <TableCell align="right" sx={HIDE_XS}>
              <TableSortLabel
                active={sortKey === 'modified'}
                direction={sortKey === 'modified' ? sortDir : 'asc'}
                onClick={() => onSort('modified')}
              >
                Modified
              </TableSortLabel>
            </TableCell>
            <TableCell align="right" width={76} />
          </TableRow>
        </TableHead>
        <TableBody>
          {rows.map((row) => (
            <TableRow key={row.id} hover sx={{ '& td': { height: 52 } }}>
              <TableCell sx={{ maxWidth: 0 }}>
                <ButtonBase
                  onClick={() => onOpen(row)}
                  sx={{
                    display: 'flex',
                    alignItems: 'center',
                    gap: 1.5,
                    width: '100%',
                    justifyContent: 'flex-start',
                    textAlign: 'left',
                    borderRadius: 1,
                  }}
                >
                  <NodeIcon name={row.name} isFolder={row.isFolder} />
                  <Box
                    component="span"
                    sx={{
                      overflow: 'hidden',
                      textOverflow: 'ellipsis',
                      whiteSpace: 'nowrap',
                      fontSize: '0.97rem',
                    }}
                  >
                    {row.name}
                  </Box>
                </ButtonBase>
              </TableCell>
              <TableCell sx={{ ...HIDE_SM, color: 'text.secondary' }}>
                {row.typeLabel}
              </TableCell>
              <TableCell
                align="right"
                sx={{
                  ...HIDE_SM,
                  color: 'text.secondary',
                  fontVariantNumeric: 'tabular-nums',
                }}
              >
                {row.sizeLabel}
              </TableCell>
              <TableCell
                align="right"
                sx={{
                  ...HIDE_XS,
                  color: 'text.secondary',
                  fontVariantNumeric: 'tabular-nums',
                }}
              >
                {row.modifiedLabel}
              </TableCell>
              <TableCell align="right">
                <RowActions row={row} onAction={onAction} />
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </TableContainer>
  )
}
