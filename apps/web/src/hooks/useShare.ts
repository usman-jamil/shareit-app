import { useQuery } from '@tanstack/react-query'
import { fetchShare } from '@/api/shareClient'
import type { Share } from '@/types'

export const shareQueryKey = (shareId: string) => ['share', shareId] as const

export const shareQueryOptions = (shareId: string) => ({
  queryKey: shareQueryKey(shareId),
  queryFn: () => fetchShare(shareId),
  // A share is immutable for its whole (short) life — never refetch it.
  staleTime: Infinity,
  retry: 1,
})

/**
 * Called from the share route component and nowhere else. Every other
 * component in the tree receives its data as props.
 */
export function useShare(shareId: string) {
  return useQuery<Share>(shareQueryOptions(shareId))
}
