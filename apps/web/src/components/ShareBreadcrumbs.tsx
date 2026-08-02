import Breadcrumbs from '@mui/material/Breadcrumbs'
import Button from '@mui/material/Button'
import type { Crumb } from '@/types'

export interface ShareBreadcrumbsProps {
  crumbs: Crumb[]
  onNavigate: (path: string[]) => void
}

export function ShareBreadcrumbs({
  crumbs,
  onNavigate,
}: ShareBreadcrumbsProps) {
  return (
    <Breadcrumbs
      separator="/"
      aria-label="Folder path"
      sx={{
        minWidth: 0,
        '& .MuiBreadcrumbs-separator': { opacity: 0.3, mx: 0.25 },
      }}
    >
      {crumbs.map((crumb, i) => {
        const isLast = i === crumbs.length - 1
        return (
          <Button
            key={crumb.path.join('/') || 'root'}
            onClick={() => onNavigate(crumb.path)}
            disabled={isLast}
            sx={{
              textTransform: 'none',
              letterSpacing: 0,
              fontWeight: 400,
              fontSize: '0.97rem',
              minWidth: 0,
              px: 0.875,
              py: 0.5,
              color: 'text.primary',
              opacity: isLast ? 1 : 0.62,
              '&.Mui-disabled': { color: 'text.primary', opacity: 1 },
            }}
          >
            {crumb.label}
          </Button>
        )
      })}
    </Breadcrumbs>
  )
}
