import { useT } from '../i18n/I18nProvider'

const icons = ['\u{1F4CA}', '⚙️', '\u{1F3AF}', '\u{1F3B2}', '\u{1F4C8}', '\u{1F6E1}️']

export function Features() {
  const { t } = useT()

  return (
    <section id="features" className="features" aria-labelledby="features-title">
      <div className="section-inner">
        <h2 id="features-title">{t.features.title}</h2>
        <p className="section-subtitle">{t.features.subtitle}</p>
        <div className="feature-grid">
          {t.features.items.map((item, index) => (
            <article className="feature-card" key={item.title}>
              <span className="feature-icon" aria-hidden="true">
                {icons[index]}
              </span>
              <h3>{item.title}</h3>
              <p>{item.desc}</p>
            </article>
          ))}
        </div>
      </div>
    </section>
  )
}
