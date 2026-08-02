import { createTheme } from '@mui/material/styles'

/**
 * Broadsheet tokens. The header and footer take `paper` and `ink` literally in
 * both colour schemes, so they always oppose each other — that contrast is the
 * page's only ornament.
 */
export const broadsheet = {
  paper: '#f3f2f2',
  ink: '#201e1d',
  cyan: '#0088b0',
  cyanLight: '#62c5ee',
  magenta: '#d6006c',
  magentaLight: '#ff458e',
  serif: '"Source Serif 4", Georgia, "Times New Roman", serif',
} as const

export const theme = createTheme({
  cssVariables: { colorSchemeSelector: 'class' },
  colorSchemes: {
    light: {
      palette: {
        primary: { main: broadsheet.cyan, dark: '#006786' },
        secondary: { main: broadsheet.magenta },
        background: { default: '#e7e5e3', paper: '#faf9f9' },
        text: { primary: broadsheet.ink, secondary: '#605d5d' },
        divider: 'rgba(32,30,29,0.14)',
        action: { hover: 'rgba(32,30,29,0.045)' },
      },
    },
    dark: {
      palette: {
        primary: { main: broadsheet.cyanLight, dark: '#38a6cf' },
        secondary: { main: broadsheet.magentaLight },
        background: { default: '#1b1a19', paper: '#232120' },
        text: { primary: '#f0eeec', secondary: '#a09b98' },
        divider: 'rgba(240,238,236,0.13)',
        action: { hover: 'rgba(240,238,236,0.055)' },
      },
    },
  },
  shape: { borderRadius: 4 },
  spacing: 8,
  typography: {
    fontFamily: broadsheet.serif,
    h1: { fontWeight: 600, letterSpacing: '-0.018em', lineHeight: 1 },
    h2: { fontWeight: 600, letterSpacing: '-0.015em', lineHeight: 1.05 },
    h3: { fontWeight: 600 },
    body1: { fontSize: '0.97rem' },
    body2: { fontSize: '0.85rem' },
    button: {
      fontWeight: 600,
      letterSpacing: '0.06em',
      textTransform: 'uppercase',
    },
    overline: { fontSize: '0.72rem', letterSpacing: '0.22em', fontWeight: 600 },
  },
  components: {
    MuiCssBaseline: {
      styleOverrides: {
        body: { WebkitFontSmoothing: 'antialiased' },
      },
    },
    MuiButton: {
      defaultProps: { disableElevation: false },
      styleOverrides: { root: { paddingInline: 22 } },
    },
    MuiToggleButton: {
      styleOverrides: { root: { textTransform: 'none', paddingInline: 12 } },
    },
    MuiTableCell: {
      styleOverrides: {
        head: {
          textTransform: 'uppercase',
          letterSpacing: '0.16em',
          fontSize: '0.72rem',
          fontWeight: 600,
        },
      },
    },
    MuiTooltip: {
      defaultProps: { arrow: true, enterDelay: 400 },
    },
  },
})
