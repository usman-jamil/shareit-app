/** Domain model for a public share. Mirrors the payload the relay returns. */

export interface FileNode {
  kind: 'file'
  name: string
  /** Bytes. */
  size: number
  /** ISO-8601, minute precision. */
  modified: string
}

export interface FolderNode {
  kind: 'folder'
  name: string
  children: ShareNode[]
}

export type ShareNode = FileNode | FolderNode

export interface Share {
  id: string
  sharedBy: string
  /** ISO-8601 instant at which the relay drops the files. */
  expiresAt: string
  /** Absolute URL the CLI printed; used for per-file direct links. */
  baseUrl: string
  root: ShareNode[]
}

export type ViewMode = 'details' | 'tiles' | 'tree'
export type SortKey = 'name' | 'modified' | 'type'
export type SortDir = 'asc' | 'desc'

/**
 * Flattened, fully formatted row handed to the view components. Views never
 * see a ShareNode — they render strings and raise events by row id.
 */
export interface RowModel {
  /** Slash-joined path from the share root; unique and stable. */
  id: string
  name: string
  isFolder: boolean
  typeLabel: string
  sizeLabel: string
  modifiedLabel: string
  /** Secondary line for tiles and tree: "12 files" or "8.2 KB". */
  metaLabel: string
  /** Tree view only. */
  depth: number
  expanded: boolean
}

export interface Crumb {
  label: string
  /** Path segments up to and including this crumb. */
  path: string[]
}
