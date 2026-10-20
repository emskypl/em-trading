import { useRef, useState, type KeyboardEvent } from 'react'
import { useT } from '../i18n/I18nProvider'

export function Screenshots() {
  const { t } = useT()
  const items = t.screenshots.items
  const [activeIndex, setActiveIndex] = useState(0)
  const dialogRef = useRef<HTMLDialogElement>(null)

  const active = items[activeIndex]

  function show(delta: number) {
    setActiveIndex((current) => (current + delta + items.length) % items.length)
  }

  function openLightbox(index: number) {
    setActiveIndex(index)
    dialogRef.current?.showModal()
  }

  function onDialogKeyDown(event: KeyboardEvent<HTMLDialogElement>) {
    if (event.key === 'ArrowRight') show(1)
    if (event.key === 'ArrowLeft') show(-1)
  }

  return (
    <section id="screenshots" className="screenshots" aria-labelledby="screenshots-title">
      <div className="section-inner">
        <h2 id="screenshots-title">{t.screenshots.title}</h2>

        <div className="screenshot-tabs" role="tablist" aria-label={t.screenshots.title}>
          {items.map((item, index) => (
            <button
              key={item.key}
              type="button"
              role="tab"
              aria-selected={index === activeIndex}
              className={index === activeIndex ? 'active' : ''}
              onClick={() => setActiveIndex(index)}
            >
              {item.caption}
            </button>
          ))}
        </div>

        <button type="button" className="screenshot-preview" onClick={() => openLightbox(activeIndex)}>
          <img src={`/screenshots/${active.file}`} alt={active.caption} width={2040} height={1290} loading="lazy" />
        </button>
      </div>

      <dialog ref={dialogRef} className="lightbox" onKeyDown={onDialogKeyDown} aria-label={active.caption}>
        <button
          type="button"
          className="lightbox-close"
          onClick={() => dialogRef.current?.close()}
          aria-label={t.screenshots.lightboxClose}
        >
          &#10005;
        </button>
        <button type="button" className="lightbox-nav prev" onClick={() => show(-1)} aria-label={t.screenshots.lightboxPrev}>
          &#8249;
        </button>
        <img src={`/screenshots/${active.file}`} alt={active.caption} />
        <button type="button" className="lightbox-nav next" onClick={() => show(1)} aria-label={t.screenshots.lightboxNext}>
          &#8250;
        </button>
        <p className="lightbox-caption">{active.caption}</p>
      </dialog>
    </section>
  )
}
