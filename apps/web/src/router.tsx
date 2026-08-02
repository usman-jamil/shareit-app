import { createRouter } from '@tanstack/react-router'
import { rootRoute } from './routes/rootRoute'
import { indexRoute } from './routes/indexRoute'
import { shareRoute } from './routes/shareRoute'
import { ErrorState } from './components/states/ErrorState'

const routeTree = rootRoute.addChildren([indexRoute, shareRoute])

export const router = createRouter({
  routeTree,
  defaultPreload: false,
  defaultNotFoundComponent: () => (
    <ErrorState
      title="No share at this address."
      body="The link may be mistyped, or the share was already collected and removed."
      onRetry={() => window.location.reload()}
    />
  ),
})

declare module '@tanstack/react-router' {
  interface Register {
    router: typeof router
  }
}
