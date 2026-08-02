import { useEffect, useState } from 'react'

/** Whole minutes left until `iso`, recomputed every 30s. Negative once past. */
export function useMinutesRemaining(iso: string): number {
  const compute = () =>
    Math.floor((new Date(iso).getTime() - Date.now()) / 60_000)
  const [minutes, setMinutes] = useState(compute)

  useEffect(() => {
    setMinutes(compute())
    const id = setInterval(() => setMinutes(compute()), 30_000)
    return () => clearInterval(id)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [iso])

  return minutes
}
