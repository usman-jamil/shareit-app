import InputAdornment from '@mui/material/InputAdornment'
import IconButton from '@mui/material/IconButton'
import TextField from '@mui/material/TextField'
import { MagnifyingGlass, X } from '@phosphor-icons/react'

export interface FilterFieldProps {
  value: string
  onChange: (value: string) => void
  placeholder?: string
}

/** Controlled filter input. Holds no state of its own. */
export function FilterField({
  value,
  onChange,
  placeholder = 'Filter files',
}: FilterFieldProps) {
  return (
    <TextField
      size="small"
      value={value}
      placeholder={placeholder}
      onChange={(e) => onChange(e.target.value)}
      inputProps={{ 'aria-label': 'Filter files in this share' }}
      sx={{
        width: 210,
        '& .MuiOutlinedInput-root': { bgcolor: 'background.paper' },
      }}
      InputProps={{
        startAdornment: (
          <InputAdornment position="start">
            <MagnifyingGlass size={16} weight="duotone" />
          </InputAdornment>
        ),
        endAdornment: value ? (
          <InputAdornment position="end">
            <IconButton
              size="small"
              aria-label="Clear filter"
              onClick={() => onChange('')}
            >
              <X size={14} weight="bold" />
            </IconButton>
          </InputAdornment>
        ) : null,
      }}
    />
  )
}
