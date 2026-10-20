import { useT } from '../i18n/I18nProvider'

export function Faq() {
  const { t } = useT()

  return (
    <section id="faq" className="faq" aria-labelledby="faq-title">
      <div className="section-inner">
        <h2 id="faq-title">{t.faq.title}</h2>
        <div className="faq-list">
          {t.faq.items.map((item) => (
            <details key={item.q}>
              <summary>{item.q}</summary>
              <p>{item.a}</p>
            </details>
          ))}
        </div>
      </div>
    </section>
  )
}
