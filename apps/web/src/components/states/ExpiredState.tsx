import Box from '@mui/material/Box'
import Typography from '@mui/material/Typography'
import { HourglassLow } from '@phosphor-icons/react'

export function ExpiredState() {
  return (
    <Box sx={{ mt: 5, maxWidth: 560 }}>
      <HourglassLow
        size={52}
        weight="duotone"
        color="var(--mui-palette-secondary-main)"
      />
      <Typography variant="h2" sx={{ mt: 2.75, fontSize: 36 }}>
        This link has expired.
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
        Shares are removed from the relay when their timer runs out — the files
        are gone from our side, not just hidden. Ask the sender to run{' '}
        <Box
          component="code"
          sx={{
            fontFamily: 'ui-monospace, SFMono-Regular, Menlo, monospace',
            fontSize: '0.9em',
            bgcolor: 'action.hover',
            px: 0.75,
            py: 0.25,
            borderRadius: 0.75,
          }}
        >
          shareit push
        </Box>{' '}
        again for a fresh address.
      </Typography>
    </Box>
  )
}
