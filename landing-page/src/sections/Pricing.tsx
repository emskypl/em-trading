import { useT } from '../i18n/I18nProvider'

export function Pricing() {
  const { t } = useT()
  const { free, pro } = t.pricing

  return (
    <section id="pricing" className="pricing" aria-labelledby="pricing-title">
      <div className="section-inner">
        <h2 id="pricing-title">{t.pricing.title}</h2>
        <p className="section-subtitle">{t.pricing.subtitle}</p>

        <div className="pricing-cards">
          <article className="pricing-card">
            <h3>{free.name}</h3>
            <p className="price">
              {free.price}
              <span className="price-period">/{free.period}</span>
            </p>
            <ul>
              {free.items.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
            <a className="button primary" href="#download">
              {free.cta}
            </a>
          </article>

          <article className="pricing-card highlight">
            <h3>{pro.name}</h3>
            <p className="price">{pro.price}</p>
            <ul>
              {pro.items.map((item) => (
                <li key={item}>{item}</li>
              ))}
            </ul>
            <a className="button" href="#faq">
              {pro.cta}
            </a>
          </article>
        </div>
      </div>
    </section>
  )
}
