import Typography from '@mui/material/Typography'
import { formatExpiry } from '@/lib/format'

export interface ExpiryNoticeProps {
  minutesRemaining: number
}

/** Deliberately quiet: one clause in the meta line, no badge, no countdown drama. */
export function ExpiryNotice({ minutesRemaining }: ExpiryNoticeProps) {
  return (
    <Typography component="span" sx={{ opacity: 0.68, fontSize: '0.97rem' }}>
      {formatExpiry(minutesRemaining)}
    </Typography>
  )
}
