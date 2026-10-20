import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react'
import { en } from './en'
import { pl } from './pl'

export type Language = 'en' | 'pl'
type Dictionary = typeof en

const dictionaries: Record<Language, Dictionary> = { en, pl }

function detectLanguage(): Language {
  const fromQuery = new URLSearchParams(window.location.search).get('lang')
  if (fromQuery === 'en' || fromQuery === 'pl') return fromQuery

  const stored = window.localStorage.getItem('lang')
  if (stored === 'en' || stored === 'pl') return stored

  return window.navigator.language.toLowerCase().startsWith('pl') ? 'pl' : 'en'
}

interface I18nContextValue {
  language: Language
  t: Dictionary
  setLanguage: (language: Language) => void
}

const I18nContext = createContext<I18nContextValue | null>(null)

export function I18nProvider({ children }: { children: ReactNode }) {
  const [language, setLanguage] = useState<Language>(detectLanguage)

  useEffect(() => {
    document.documentElement.lang = language
    window.localStorage.setItem('lang', language)
  }, [language])

  const value = useMemo<I18nContextValue>(
    () => ({ language, t: dictionaries[language], setLanguage }),
    [language],
  )

  return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>
}

export function useT() {
  const context = useContext(I18nContext)
  if (!context) throw new Error('useT must be used within an I18nProvider')
  return context
}
