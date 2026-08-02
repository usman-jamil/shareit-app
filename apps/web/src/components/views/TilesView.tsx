import Box from '@mui/material/Box'
import ButtonBase from '@mui/material/ButtonBase'
import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import type { RowModel } from '@/types'
import { NodeIcon } from '../NodeIcon'
import { RowActions } from '../RowActions'

export interface TilesViewProps {
  rows: RowModel[]
  onOpen: (row: RowModel) => void
  onAction: (row: RowModel) => void
}

export function TilesView({ rows, onOpen, onAction }: TilesViewProps) {
  return (
    <Box
      sx={{
        mt: 2.25,
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(186px, 1fr))',
        gap: 1.75,
      }}
    >
      {rows.map((row) => (
        <Paper
          key={row.id}
          elevation={1}
          sx={{
            display: 'flex',
            flexDirection: 'column',
            transition: 'box-shadow 120ms ease',
            '&:hover': { boxShadow: 4 },
          }}
        >
          <ButtonBase
            onClick={() => onOpen(row)}
            sx={{
              flex: 1,
              display: 'flex',
              flexDirection: 'column',
              alignItems: 'flex-start',
              gap: 1.75,
              p: '20px 18px 16px',
              width: '100%',
              textAlign: 'left',
              borderRadius: 1,
            }}
          >
            <NodeIcon name={row.name} isFolder={row.isFolder} size={36} />
            <Typography
              component="span"
              sx={{
                fontSize: '0.97rem',
                lineHeight: 1.28,
                wordBreak: 'break-word',
                textWrap: 'pretty',
              }}
            >
              {row.name}
            </Typography>
          </ButtonBase>
          <Stack
            direction="row"
            alignItems="center"
            justifyContent="space-between"
            gap={1}
            sx={{ px: 2.25, pb: 1.75, color: 'text.secondary' }}
          >
            <Typography variant="body2">{row.metaLabel}</Typography>
            <RowActions row={row} size={28} onAction={onAction} />
          </Stack>
        </Paper>
      ))}
    </Box>
  )
}
