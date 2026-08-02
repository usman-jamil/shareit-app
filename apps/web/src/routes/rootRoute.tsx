import { Outlet, createRootRoute } from '@tanstack/react-router'
import Box from '@mui/material/Box'
import { ShareFooter } from '@/components/ShareFooter'
import { APP_NAME, APP_OWNER, APP_VERSION } from '@/lib/constants'

/**
 * App shell. The footer is static chrome and lives here; the masthead needs
 * share data, so it is rendered by the share route.
 */
function RootLayout() {
  return (
    <Box
      sx={{
        minHeight: '100vh',
        display: 'flex',
        flexDirection: 'column',
        bgcolor: 'background.default',
      }}
    >
      <Outlet />
      <ShareFooter appName={APP_NAME} version={APP_VERSION} owner={APP_OWNER} />
    </Box>
  )
}

export const rootRoute = createRootRoute({ component: RootLayout })
