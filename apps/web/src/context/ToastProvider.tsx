import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from 'react'
import Snackbar from '@mui/material/Snackbar'
import Alert from '@mui/material/Alert'

interface ToastApi {
  notify: (message: string) => void
}

const ToastContext = createContext<ToastApi | null>(null)

/**
 * Transient confirmations ("Link copied", "Zipping docs…"). Lives above the
 * router so any route can raise one; presentational components never call it
 * directly — they raise a callback and the route decides.
 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [message, setMessage] = useState<string | null>(null)
  const seq = useRef(0)

  const notify = useCallback((next: string) => {
    seq.current += 1
    setMessage(next)
  }, [])

  const api = useMemo(() => ({ notify }), [notify])

  return (
    <ToastContext.Provider value={api}>
      {children}
      <Snackbar
        key={seq.current}
        open={message !== null}
        autoHideDuration={2400}
        onClose={() => setMessage(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
      >
        <Alert
          severity="success"
          variant="filled"
          onClose={() => setMessage(null)}
        >
          {message}
        </Alert>
      </Snackbar>
    </ToastContext.Provider>
  )
}

export function useToast(): ToastApi {
  const ctx = useContext(ToastContext)
  if (!ctx) throw new Error('useToast must be used inside <ToastProvider>')
  return ctx
}
