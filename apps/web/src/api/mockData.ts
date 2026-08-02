import type { Share, ShareNode } from '@/types'

const folder = (name: string, children: ShareNode[]): ShareNode => ({
  kind: 'folder',
  name,
  children,
})

const file = (name: string, size: number, modified: string): ShareNode => ({
  kind: 'file',
  name,
  size,
  modified,
})

/** One mocked share. Swap for the real relay response; the shape is identical. */
export const MOCK_SHARE: Share = {
  id: 'k7m2qd',
  sharedBy: 'Zuhaib Sohail',
  expiresAt: new Date(Date.now() + 42 * 60_000).toISOString(),
  baseUrl: 'https://share.it/s/k7m2qd',
  root: [
    folder('src', [
      folder('api', [
        file('client.ts', 8214, '2026-07-30T14:22'),
        file('schema.ts', 23901, '2026-07-30T14:22'),
        file('retry.ts', 3120, '2026-07-28T09:05'),
      ]),
      folder('components', [
        file('ShareHeader.tsx', 6482, '2026-07-31T18:40'),
        file('FileGrid.tsx', 11204, '2026-07-31T18:40'),
        file('TreeView.tsx', 9375, '2026-07-31T11:12'),
        file('theme.ts', 2210, '2026-07-29T16:03'),
      ]),
      file('index.tsx', 1840, '2026-08-01T08:15'),
      file('app.css', 5120, '2026-07-27T13:44'),
    ]),
    folder('dist', [
      folder('assets', [
        file('index-4f2b9c.js', 284310, '2026-08-01T09:02'),
        file('index-4f2b9c.css', 41208, '2026-08-01T09:02'),
        file('logo-9ac1.svg', 3402, '2026-08-01T09:02'),
      ]),
      file('index.html', 1204, '2026-08-01T09:02'),
    ]),
    folder('docs', [
      file('architecture.md', 14822, '2026-07-24T10:30'),
      file('cli-reference.md', 9640, '2026-07-24T10:30'),
      file('diagram.png', 186422, '2026-07-23T17:55'),
    ]),
    folder('.github', [
      folder('workflows', [
        file('ci.yml', 2418, '2026-07-20T12:00'),
        file('release.yml', 1902, '2026-07-20T12:00'),
      ]),
    ]),
    file('README.md', 4820, '2026-08-01T09:14'),
    file('package.json', 2140, '2026-08-01T08:52'),
    file('docker-compose.yml', 1620, '2026-07-26T15:10'),
    file('coverage.lcov', 98204, '2026-08-01T09:03'),
  ],
}

/** An empty share — the sender pushed a folder with nothing in it. */
export const MOCK_EMPTY_SHARE: Share = {
  ...MOCK_SHARE,
  id: 'empty1',
  baseUrl: 'https://share.it/s/empty1',
  root: [],
}

/** A share whose timer already ran out. */
export const MOCK_EXPIRED_SHARE: Share = {
  ...MOCK_SHARE,
  id: 'gone01',
  baseUrl: 'https://share.it/s/gone01',
  expiresAt: new Date(Date.now() - 5 * 60_000).toISOString(),
}

export const MOCK_SHARES: Record<string, Share> = {
  [MOCK_SHARE.id]: MOCK_SHARE,
  [MOCK_EMPTY_SHARE.id]: MOCK_EMPTY_SHARE,
  [MOCK_EXPIRED_SHARE.id]: MOCK_EXPIRED_SHARE,
}
