import Paper from '@mui/material/Paper'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { FolderDashed } from '@phosphor-icons/react'

export interface EmptyStateProps {
  title: string
  body: string
}

export function EmptyState({ title, body }: EmptyStateProps) {
  return (
    <Paper elevation={1} sx={{ mt: 2.25, py: 11, px: 3.5 }}>
      <Stack alignItems="center" gap={1}>
        <FolderDashed
          size={46}
          weight="duotone"
          color="var(--mui-palette-text-secondary)"
        />
        <Typography variant="h3" sx={{ mt: 1.5, fontSize: 22 }}>
          {title}
        </Typography>
        <Typography sx={{ color: 'text.secondary' }}>{body}</Typography>
      </Stack>
    </Paper>
  )
}
