import type { RowModel, ShareNode, SortDir, SortKey } from '@/types'
import { formatCount, formatDate, formatSize } from './format'
import {
  childrenAt,
  countFiles,
  isFolder,
  labelOf,
  matchesQuery,
  modifiedOf,
  sizeOf,
  sortNodes,
} from './tree'

interface RowOptions {
  sortKey: SortKey
  sortDir: SortDir
  /** Raw filter text; lowercased internally. */
  query: string
}

function toRow(
  node: ShareNode,
  path: string[],
  depth: number,
  expanded: boolean
): RowModel {
  const bytes = sizeOf(node)
  return {
    id: [...path, node.name].join('/'),
    name: node.name,
    isFolder: isFolder(node),
    typeLabel: labelOf(node),
    sizeLabel: formatSize(bytes),
    modifiedLabel: formatDate(modifiedOf(node)),
    metaLabel: isFolder(node)
      ? formatCount(countFiles(node.children), 'file')
      : formatSize(bytes),
    depth,
    expanded,
  }
}

/** Rows for the current folder — what tiles and details render. */
export function buildFolderRows(
  root: ShareNode[],
  path: string[],
  { sortKey, sortDir, query }: RowOptions
): RowModel[] {
  const q = query.trim().toLowerCase()
  const visible = childrenAt(root, path).filter((n) => matchesQuery(n, q))
  return sortNodes(visible, sortKey, sortDir).map((n) =>
    toRow(n, path, 0, false)
  )
}

/**
 * Rows for the tree — the whole share flattened, honouring expansion. A live
 * filter expands everything so matches deep in the tree are reachable.
 */
export function buildTreeRows(
  root: ShareNode[],
  expandedIds: ReadonlySet<string>,
  { sortKey, sortDir, query }: RowOptions
): RowModel[] {
  const q = query.trim().toLowerCase()
  const out: RowModel[] = []

  const walk = (nodes: ShareNode[], path: string[], depth: number) => {
    for (const node of sortNodes(nodes, sortKey, sortDir)) {
      if (!matchesQuery(node, q)) continue
      const id = [...path, node.name].join('/')
      const expanded = isFolder(node) && (q !== '' || expandedIds.has(id))
      out.push(toRow(node, path, depth, expanded))
      if (isFolder(node) && expanded)
        walk(node.children, [...path, node.name], depth + 1)
    }
  }

  walk(root, [], 0)
  return out
}

/** "src/api/client.ts" -> ["src", "api", "client.ts"] */
export const splitPath = (id: string): string[] => (id ? id.split('/') : [])
