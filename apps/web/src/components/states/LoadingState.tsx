import Skeleton from '@mui/material/Skeleton'
import Stack from '@mui/material/Stack'

/** Placeholder while the root page's query is in flight. */
export function LoadingState({ rows = 7 }: { rows?: number }) {
  return (
    <Stack gap={1} sx={{ mt: 2.25 }}>
      <Skeleton variant="rectangular" height={44} />
      {Array.from({ length: rows }, (_, i) => (
        <Skeleton key={i} variant="rectangular" height={52} />
      ))}
    </Stack>
  )
}
