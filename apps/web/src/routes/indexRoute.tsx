import { createRoute, redirect } from '@tanstack/react-router'
import { rootRoute } from './rootRoute'
import { DEMO_SHARE_ID } from '@/lib/constants'

/**
 * There is no home page — a bare `/` is only ever hit in development, so it
 * bounces to the fixture share.
 */
export const indexRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/',
  beforeLoad: () => {
    throw redirect({
      to: '/s/$shareId',
      params: { shareId: DEMO_SHARE_ID },
      search: {} as never,
    })
  },
})
