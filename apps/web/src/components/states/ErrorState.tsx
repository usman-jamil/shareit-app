import Box from '@mui/material/Box'
import Button from '@mui/material/Button'
import Typography from '@mui/material/Typography'
import { ArrowClockwise, Plug } from '@phosphor-icons/react'

export interface ErrorStateProps {
  title?: string
  body?: string
  retrying?: boolean
  onRetry: () => void
}

export function ErrorState({
  title = "We couldn't reach the relay.",
  body = "The share exists, but the node holding it didn't answer. This is usually brief.",
  retrying = false,
  onRetry,
}: ErrorStateProps) {
  return (
    <Box sx={{ mt: 5, maxWidth: 560 }}>
      <Plug
        size={52}
        weight="duotone"
        color="var(--mui-palette-secondary-main)"
      />
      <Typography variant="h2" sx={{ mt: 2.75, fontSize: 36 }}>
        {title}
      </Typography>
      <Typography
        sx={{
          mt: 1.75,
          fontSize: '1.03rem',
          lineHeight: 1.6,
          color: 'text.secondary',
          textWrap: 'pretty',
        }}
      >
        {body}
      </Typography>
      <Button
        variant="outlined"
        onClick={onRetry}
        disabled={retrying}
        startIcon={<ArrowClockwise size={17} weight="duotone" />}
        sx={{ mt: 3 }}
      >
        {retrying ? 'Retrying…' : 'Try again'}
      </Button>
    </Box>
  )
}
