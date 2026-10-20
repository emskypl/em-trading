import { useEffect, useState } from 'react'

export interface LatestRelease {
  version: string
  publishedAt: string
  assetName: string
  assetUrl: string
  sizeBytes: number
  shaAssetUrl?: string
}

type Status = 'loading' | 'ready' | 'error'

const CACHE_KEY = 'emtrading-latest-release'
const RELEASES_API_URL = 'https://api.github.com/repos/emskypl/em-trading/releases/latest'

interface GitHubAsset {
  name: string
  size: number
  browser_download_url: string
}

interface GitHubRelease {
  tag_name: string
  published_at: string
  assets: GitHubAsset[]
}

export function useLatestRelease() {
  const [release, setRelease] = useState<LatestRelease | null>(null)
  const [status, setStatus] = useState<Status>('loading')

  useEffect(() => {
    const cached = window.sessionStorage.getItem(CACHE_KEY)
    if (cached) {
      setRelease(JSON.parse(cached) as LatestRelease)
      setStatus('ready')
      return
    }

    let cancelled = false

    fetch(RELEASES_API_URL, { headers: { Accept: 'application/vnd.github+json' } })
      .then((response) => {
        if (!response.ok) throw new Error(`GitHub API returned ${response.status}`)
        return response.json() as Promise<GitHubRelease>
      })
      .then((data) => {
        if (cancelled) return

        const zipAsset = data.assets.find((asset) => asset.name.endsWith('.zip'))
        if (!zipAsset) throw new Error('The latest release has no .zip asset')
        const shaAsset = data.assets.find((asset) => asset.name.endsWith('.sha256'))

        const parsed: LatestRelease = {
          version: data.tag_name,
          publishedAt: data.published_at,
          assetName: zipAsset.name,
          assetUrl: zipAsset.browser_download_url,
          sizeBytes: zipAsset.size,
          shaAssetUrl: shaAsset?.browser_download_url,
        }

        window.sessionStorage.setItem(CACHE_KEY, JSON.stringify(parsed))
        setRelease(parsed)
        setStatus('ready')
      })
      .catch(() => {
        if (!cancelled) setStatus('error')
      })

    return () => {
      cancelled = true
    }
  }, [])

  return { release, status }
}
