import {
  BracketsCurly,
  ChartBar,
  File,
  FileCss,
  FileHtml,
  FileImage,
  FileJs,
  FileMd,
  FileSvg,
  FileTs,
  FileTsx,
  Folder,
  GearSix,
  type Icon,
} from '@phosphor-icons/react'
import { extensionOf } from '@/lib/format'

const BY_EXTENSION: Record<string, Icon> = {
  ts: FileTs,
  tsx: FileTsx,
  js: FileJs,
  jsx: FileJs,
  json: BracketsCurly,
  md: FileMd,
  css: FileCss,
  html: FileHtml,
  svg: FileSvg,
  png: FileImage,
  jpg: FileImage,
  jpeg: FileImage,
  yml: GearSix,
  yaml: GearSix,
  lcov: ChartBar,
}

export interface NodeIconProps {
  name: string
  isFolder: boolean
  size?: number
}

/** Duotone glyph for a node, chosen from its extension. Purely presentational. */
export function NodeIcon({ name, isFolder, size = 21 }: NodeIconProps) {
  const Glyph = isFolder ? Folder : (BY_EXTENSION[extensionOf(name)] ?? File)
  return (
    <Glyph
      size={size}
      weight="duotone"
      color={
        isFolder
          ? 'var(--mui-palette-primary-main)'
          : 'var(--mui-palette-text-secondary)'
      }
      style={{ flex: 'none' }}
    />
  )
}
