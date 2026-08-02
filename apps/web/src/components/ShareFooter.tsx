import Box from '@mui/material/Box'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { broadsheet } from '@/theme'

export interface ShareFooterProps {
  appName: string
  version: string
  owner: string
}

/** The colophon. Always ink-on-paper-white type — the header's opposite. */
export function ShareFooter({ appName, version, owner }: ShareFooterProps) {
  return (
    <Box
      component="footer"
      sx={{
        bgcolor: broadsheet.ink,
        color: broadsheet.paper,
        borderTop: `4px solid ${broadsheet.magenta}`,
      }}
    >
      <Stack
        direction="row"
        flexWrap="wrap"
        alignItems="flex-end"
        justifyContent="space-between"
        gap={3.5}
        sx={{ maxWidth: 1180, mx: 'auto', px: 3.5, pt: 4.5, pb: 5 }}
      >
        <Box sx={{ minWidth: 0 }}>
          <Typography
            component="div"
            sx={{
              fontWeight: 600,
              fontSize: 'clamp(34px, 5.4vw, 54px)',
              lineHeight: 0.94,
              letterSpacing: '-0.022em',
            }}
          >
            {appName}
          </Typography>
          <Typography
            sx={{
              mt: 1.5,
              maxWidth: '46ch',
              fontSize: 14,
              lineHeight: 1.6,
              opacity: 0.62,
              textWrap: 'pretty',
            }}
          >
            Internal file relay. Anyone holding this address can read the share
            — no account, no sign-in, no trace of who looked.
          </Typography>
        </Box>
        <Stack
          gap={0.875}
          sx={{
            textAlign: 'right',
            fontSize: 11.5,
            letterSpacing: '0.18em',
            textTransform: 'uppercase',
            opacity: 0.5,
          }}
        >
          <span>cli · api · web</span>
          <span>{version}</span>
          <span>{owner}</span>
        </Stack>
      </Stack>
    </Box>
  )
}
