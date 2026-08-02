import Button from '@mui/material/Button'
import { FileZip } from '@phosphor-icons/react'
import { broadsheet } from '@/theme'

export interface DownloadZipButtonProps {
  fileCount: number
  disabled?: boolean
  onDownload: () => void
}

export function DownloadZipButton({
  fileCount,
  disabled,
  onDownload,
}: DownloadZipButtonProps) {
  return (
    <Button
      variant="contained"
      size="large"
      disabled={disabled}
      onClick={onDownload}
      startIcon={<FileZip size={19} weight="duotone" />}
      aria-label={`Download all ${fileCount} files as a zip archive`}
      sx={{
        // Fixed cyan against the paper masthead in both colour schemes.
        bgcolor: broadsheet.cyan,
        color: '#fff',
        whiteSpace: 'nowrap',
        '&:hover': { bgcolor: '#006786' },
      }}
    >
      Download .zip
    </Button>
  )
}
