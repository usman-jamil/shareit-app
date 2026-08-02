import Box from '@mui/material/Box'
import Stack from '@mui/material/Stack'
import Typography from '@mui/material/Typography'
import { PaperPlaneTilt } from '@phosphor-icons/react'
import { broadsheet } from '@/theme'
import { DownloadZipButton } from './DownloadZipButton'
import { ExpiryNotice } from './ExpiryNotice'
import { ThemeModeToggle } from './ThemeModeToggle'

export interface ShareHeaderProps {
  appName: string
  sharedBy: string
  /** Files only — folders are not counted. */
  fileCount: number
  totalSizeLabel: string
  minutesRemaining: number
  downloadDisabled?: boolean
  onDownloadAll: () => void
}

/**
 * The masthead. Always prints ink-on-paper, whatever the colour scheme, so it
 * sits opposite the ink footer.
 */
export function ShareHeader({
  appName,
  sharedBy,
  fileCount,
  totalSizeLabel,
  minutesRemaining,
  downloadDisabled,
  onDownloadAll,
}: ShareHeaderProps) {
  return (
    <Box
      component="header"
      sx={{
        bgcolor: broadsheet.paper,
        color: broadsheet.ink,
        borderBottom: '1px solid rgba(32,30,29,0.2)',
      }}
    >
      <Box sx={{ maxWidth: 1180, mx: 'auto', px: 3.5, pt: 2, pb: 3.75 }}>
        <Stack
          direction="row"
          alignItems="center"
          justifyContent="space-between"
          gap={2}
        >
          <Stack direction="row" alignItems="center" gap={1.5}>
            <PaperPlaneTilt
              size={19}
              weight="duotone"
              color={broadsheet.cyan}
            />
            <Typography
              variant="overline"
              sx={{ letterSpacing: '0.24em', lineHeight: 1 }}
            >
              {appName}
            </Typography>
            <Box
              sx={{ width: '1px', height: 13, bgcolor: 'rgba(32,30,29,0.28)' }}
            />
            <Typography
              sx={{
                fontSize: 12,
                letterSpacing: '0.14em',
                textTransform: 'uppercase',
                opacity: 0.55,
              }}
            >
              Public link
            </Typography>
          </Stack>
          <ThemeModeToggle />
        </Stack>

        <Stack
          direction="row"
          flexWrap="wrap"
          alignItems="flex-end"
          justifyContent="space-between"
          gap={3.5}
          sx={{ mt: 3.75 }}
        >
          <Box sx={{ minWidth: 0 }}>
            <Typography
              variant="overline"
              sx={{ opacity: 0.52, display: 'block' }}
            >
              Shared by
            </Typography>
            <Typography
              variant="h1"
              sx={{ mt: 1, fontSize: 'clamp(32px, 5vw, 52px)' }}
            >
              {sharedBy}
            </Typography>
            <Stack
              direction="row"
              flexWrap="wrap"
              alignItems="center"
              gap={1.25}
              sx={{ mt: 1.75 }}
            >
              <Typography
                component="span"
                sx={{ fontWeight: 600, fontSize: '0.97rem' }}
              >
                {fileCount} {fileCount === 1 ? 'file' : 'files'}
              </Typography>
              <Box component="span" sx={{ opacity: 0.34 }}>
                /
              </Box>
              <Typography
                component="span"
                sx={{ opacity: 0.68, fontSize: '0.97rem' }}
              >
                {totalSizeLabel}
              </Typography>
              <Box component="span" sx={{ opacity: 0.34 }}>
                /
              </Box>
              <ExpiryNotice minutesRemaining={minutesRemaining} />
            </Stack>
          </Box>

          <DownloadZipButton
            fileCount={fileCount}
            disabled={downloadDisabled}
            onDownload={onDownloadAll}
          />
        </Stack>
      </Box>
    </Box>
  )
}
