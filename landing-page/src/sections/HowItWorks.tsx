import { useT } from '../i18n/I18nProvider'

export function HowItWorks() {
  const { t } = useT()

  return (
    <section id="how-it-works" className="how-it-works" aria-labelledby="how-it-works-title">
      <div className="section-inner">
        <h2 id="how-it-works-title">{t.howItWorks.title}</h2>
        <ol className="steps">
          {t.howItWorks.steps.map((step, index) => (
            <li key={step.title}>
              <span className="step-number" aria-hidden="true">
                {index + 1}
              </span>
              <h3>{step.title}</h3>
              <p>{step.desc}</p>
            </li>
          ))}
        </ol>
      </div>
    </section>
  )
}
