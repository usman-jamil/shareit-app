import { useCallback, useMemo, useState } from 'react'
import { createRoute, useNavigate } from '@tanstack/react-router'
import Box from '@mui/material/Box'
import { rootRoute } from './rootRoute'
import { useShare } from '@/hooks/useShare'
import { useMinutesRemaining } from '@/hooks/useMinutesRemaining'
import { useToast } from '@/context/ToastProvider'
import { fileUrl, zipUrl } from '@/api/shareClient'
import { buildFolderRows, buildTreeRows, splitPath } from '@/lib/rows'
import { countFiles, totalSize } from '@/lib/tree'
import { formatSize } from '@/lib/format'
import { APP_NAME } from '@/lib/constants'
import type { Crumb, RowModel, SortDir, SortKey, ViewMode } from '@/types'
import { ShareHeader } from '@/components/ShareHeader'
import { ShareToolbar } from '@/components/ShareToolbar'
import { DetailsView } from '@/components/views/DetailsView'
import { TilesView } from '@/components/views/TilesView'
import { TreeView } from '@/components/views/TreeView'
import { EmptyState } from '@/components/states/EmptyState'
import { ErrorState } from '@/components/states/ErrorState'
import { ExpiredState } from '@/components/states/ExpiredState'
import { LoadingState } from '@/components/states/LoadingState'

/** Browsing state lives in the URL, so any view of a share is linkable. */
export interface ShareSearch {
  view: ViewMode
  sort: SortKey
  dir: SortDir
  q: string
  /** Slash-joined folder path inside the share. */
  path: string
}

const VIEWS: ViewMode[] = ['details', 'tiles', 'tree']
const SORTS: SortKey[] = ['name', 'modified', 'type']

function validateSearch(raw: Record<string, unknown>): ShareSearch {
  const view = VIEWS.includes(raw.view as ViewMode)
    ? (raw.view as ViewMode)
    : 'details'
  const sort = SORTS.includes(raw.sort as SortKey)
    ? (raw.sort as SortKey)
    : 'name'
  const dir: SortDir = raw.dir === 'desc' ? 'desc' : 'asc'
  return {
    view,
    sort,
    dir,
    q: typeof raw.q === 'string' ? raw.q : '',
    path: typeof raw.path === 'string' ? raw.path : '',
  }
}

/**
 * The share page — the only component in the app that talks to the network.
 * Everything below it receives formatted data and callbacks as props.
 */
function SharePage() {
  const { shareId } = shareRoute.useParams()
  const search = shareRoute.useSearch()
  const navigate = useNavigate({ from: shareRoute.fullPath })
  const { notify } = useToast()

  const {
    data: share,
    isPending,
    isError,
    refetch,
    isFetching,
  } = useShare(shareId)
  const [expandedIds, setExpandedIds] = useState<ReadonlySet<string>>(
    () => new Set(['src', 'src/api'])
  )

  const patchSearch = useCallback(
    (patch: Partial<ShareSearch>, replace = false) => {
      void navigate({
        search: (prev: ShareSearch) => ({ ...prev, ...patch }),
        replace,
      })
    },
    [navigate]
  )

  const path = useMemo(() => splitPath(search.path), [search.path])
  const minutesRemaining = useMinutesRemaining(
    share?.expiresAt ?? new Date().toISOString()
  )

  const rows = useMemo(
    () =>
      share
        ? search.view === 'tree'
          ? buildTreeRows(share.root, expandedIds, {
              sortKey: search.sort,
              sortDir: search.dir,
              query: search.q,
            })
          : buildFolderRows(share.root, path, {
              sortKey: search.sort,
              sortDir: search.dir,
              query: search.q,
            })
        : [],
    [share, search.view, search.sort, search.dir, search.q, path, expandedIds]
  )

  const crumbs = useMemo<Crumb[]>(
    () => [
      { label: 'Share root', path: [] },
      ...path.map((segment, i) => ({
        label: segment,
        path: path.slice(0, i + 1),
      })),
    ],
    [path]
  )

  const openRow = useCallback(
    (row: RowModel) => {
      if (!share) return
      if (row.isFolder) {
        patchSearch({ path: row.id, q: '' })
        return
      }
      // Files download on click — the relay serves them with Content-Disposition.
      window.location.assign(fileUrl(share, splitPath(row.id)))
      notify(`Downloading ${row.name}`)
    },
    [share, patchSearch, notify]
  )

  const runRowAction = useCallback(
    async (row: RowModel) => {
      if (!share) return
      if (row.isFolder) {
        window.location.assign(zipUrl(share, splitPath(row.id)))
        notify(`Zipping ${row.name}…`)
        return
      }
      await navigator.clipboard.writeText(fileUrl(share, splitPath(row.id)))
      notify(`Link to ${row.name} copied`)
    },
    [share, notify]
  )

  const toggleRow = useCallback((row: RowModel) => {
    setExpandedIds((prev) => {
      const next = new Set(prev)
      if (next.has(row.id)) next.delete(row.id)
      else next.add(row.id)
      return next
    })
  }, [])

  const sortByColumn = useCallback(
    (key: SortKey) =>
      patchSearch({
        sort: key,
        dir: search.sort === key && search.dir === 'asc' ? 'desc' : 'asc',
      }),
    [patchSearch, search.sort, search.dir]
  )

  const downloadAll = useCallback(() => {
    if (!share) return
    window.location.assign(zipUrl(share))
    notify(`Preparing share.zip — ${countFiles(share.root)} files`)
  }, [share, notify])

  const fileCount = share ? countFiles(share.root) : 0
  const expired = !!share && minutesRemaining <= 0

  return (
    <>
      <ShareHeader
        appName={APP_NAME}
        sharedBy={share?.sharedBy ?? '…'}
        fileCount={fileCount}
        totalSizeLabel={share ? formatSize(totalSize(share.root)) : '—'}
        minutesRemaining={minutesRemaining}
        downloadDisabled={!share || expired || fileCount === 0}
        onDownloadAll={downloadAll}
      />

      <Box
        component="main"
        sx={{
          flex: 1,
          width: '100%',
          maxWidth: 1180,
          mx: 'auto',
          px: 3.5,
          pt: 3.25,
          pb: 8,
        }}
      >
        {isPending && <LoadingState />}

        {isError && (
          <ErrorState retrying={isFetching} onRetry={() => void refetch()} />
        )}

        {share && expired && <ExpiredState />}

        {share && !expired && (
          <>
            <ShareToolbar
              crumbs={crumbs}
              query={search.q}
              sortKey={search.sort}
              sortDir={search.dir}
              view={search.view}
              onNavigate={(next) =>
                patchSearch({ path: next.join('/'), q: '' })
              }
              onQueryChange={(q) => patchSearch({ q }, true)}
              onSortKeyChange={(sort) => patchSearch({ sort })}
              onSortDirToggle={() =>
                patchSearch({ dir: search.dir === 'asc' ? 'desc' : 'asc' })
              }
              onViewChange={(view) => patchSearch({ view })}
            />

            {rows.length === 0 && (
              <EmptyState
                title={
                  search.q
                    ? `Nothing matches “${search.q}”`
                    : 'This folder is empty'
                }
                body={
                  search.q
                    ? 'Clear the filter to see the rest of the share.'
                    : 'The sender pushed it without any contents.'
                }
              />
            )}

            {rows.length > 0 && search.view === 'details' && (
              <DetailsView
                rows={rows}
                sortKey={search.sort}
                sortDir={search.dir}
                onSort={sortByColumn}
                onOpen={openRow}
                onAction={runRowAction}
              />
            )}

            {rows.length > 0 && search.view === 'tiles' && (
              <TilesView rows={rows} onOpen={openRow} onAction={runRowAction} />
            )}

            {rows.length > 0 && search.view === 'tree' && (
              <TreeView
                rows={rows}
                onToggle={toggleRow}
                onOpen={openRow}
                onAction={runRowAction}
              />
            )}
          </>
        )}
      </Box>
    </>
  )
}

export const shareRoute = createRoute({
  getParentRoute: () => rootRoute,
  path: '/s/$shareId',
  validateSearch,
  component: SharePage,
})
