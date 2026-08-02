import type { FolderNode, ShareNode, SortDir, SortKey } from '@/types'
import { typeLabelFor } from './format'

export const isFolder = (node: ShareNode): node is FolderNode =>
  node.kind === 'folder'

export function countFiles(nodes: ShareNode[]): number {
  return nodes.reduce(
    (sum, n) => sum + (isFolder(n) ? countFiles(n.children) : 1),
    0
  )
}

export function sizeOf(node: ShareNode): number {
  return isFolder(node)
    ? node.children.reduce((sum, c) => sum + sizeOf(c), 0)
    : node.size
}

export function totalSize(nodes: ShareNode[]): number {
  return nodes.reduce((sum, n) => sum + sizeOf(n), 0)
}

/** A folder inherits the most recent modification time beneath it. */
export function modifiedOf(node: ShareNode): string {
  if (!isFolder(node)) return node.modified
  return node.children.reduce((latest, c) => {
    const t = modifiedOf(c)
    return t > latest ? t : latest
  }, '')
}

export function childrenAt(root: ShareNode[], path: string[]): ShareNode[] {
  let level = root
  for (const segment of path) {
    const next = level.find((n) => isFolder(n) && n.name === segment)
    if (!next || !isFolder(next)) return []
    level = next.children
  }
  return level
}

/** True if the node or anything beneath it matches the (lowercased) query. */
export function matchesQuery(node: ShareNode, query: string): boolean {
  if (!query) return true
  if (node.name.toLowerCase().includes(query)) return true
  return isFolder(node) && node.children.some((c) => matchesQuery(c, query))
}

/** Folders always lead, whatever the sort direction — standard file-manager behaviour. */
export function sortNodes(
  nodes: ShareNode[],
  key: SortKey,
  dir: SortDir
): ShareNode[] {
  const factor = dir === 'asc' ? 1 : -1
  return [...nodes].sort((a, b) => {
    if (isFolder(a) !== isFolder(b)) return isFolder(a) ? -1 : 1
    let result: number
    switch (key) {
      case 'modified':
        result = modifiedOf(a).localeCompare(modifiedOf(b))
        break
      case 'type':
        result =
          labelOf(a).localeCompare(labelOf(b)) ||
          a.name.localeCompare(b.name, undefined, { numeric: true })
        break
      default:
        result = a.name.localeCompare(b.name, undefined, { numeric: true })
    }
    return result * factor
  })
}

export function labelOf(node: ShareNode): string {
  return isFolder(node) ? 'Folder' : typeLabelFor(node.name)
}
