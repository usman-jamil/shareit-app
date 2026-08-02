import Button from '@mui/material/Button'
import { MoonStars, Sun } from '@phosphor-icons/react'
import { useColorScheme } from '@mui/material/styles'

/**
 * The one component that reads MUI's colour-scheme context rather than props —
 * it owns no share data, only the appearance preference.
 */
export function ThemeModeToggle() {
  const { mode, setMode } = useColorScheme()
  const isLight = mode === 'light'

  return (
    <Button
      size="small"
      onClick={() => setMode(isLight ? 'dark' : 'light')}
      startIcon={
        isLight ? (
          <MoonStars size={15} weight="duotone" />
        ) : (
          <Sun size={15} weight="duotone" />
        )
      }
      sx={{
        color: 'inherit',
        border: '1px solid rgba(32,30,29,0.24)',
        letterSpacing: '0.1em',
        fontSize: 12,
        px: 1.5,
        '&:hover': { bgcolor: 'rgba(32,30,29,0.06)' },
      }}
    >
      {isLight ? 'Dark' : 'Light'}
    </Button>
  )
}
