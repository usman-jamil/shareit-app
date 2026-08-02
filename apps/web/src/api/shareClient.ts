import type { Share } from '@/types'
import { MOCK_SHARES } from './mockData'

export class ShareNotFoundError extends Error {
  constructor(id: string) {
    super(`No share at ${id}`)
    this.name = 'ShareNotFoundError'
  }
}

export class ShareUnreachableError extends Error {
  constructor() {
    super('The relay node did not answer')
    this.name = 'ShareUnreachableError'
  }
}

const delay = (ms: number) => new Promise((r) => setTimeout(r, ms))

/**
 * The only network boundary in the app. Today it resolves from a fixture after
 * a short delay; replacing the body with `fetch(...)` is the whole migration.
 *
 * Reserved ids while mocking:
 *   `err500` — always rejects, to exercise the error state
 *   `empty1` — resolves to a share with no contents
 *   `gone01` — resolves to a share whose expiry is in the past
 */
export async function fetchShare(shareId: string): Promise<Share> {
  await delay(420)

  if (shareId === 'err500') throw new ShareUnreachableError()

  const share = MOCK_SHARES[shareId]
  if (!share) throw new ShareNotFoundError(shareId)

  return share
}

/**
 * Kicks off the archive download. The real implementation navigates to the
 * relay's zip endpoint; nothing to mock beyond the URL it would hit.
 */
export function zipUrl(share: Share, path: string[] = []): string {
  const suffix = path.length
    ? `?path=${encodeURIComponent(path.join('/'))}`
    : ''
  return `${share.baseUrl}/archive.zip${suffix}`
}

/** Direct, permanent-until-expiry link to a single file. */
export function fileUrl(share: Share, path: string[]): string {
  return `${share.baseUrl}/raw/${path.map(encodeURIComponent).join('/')}`
}
