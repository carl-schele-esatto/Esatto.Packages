// "How to connect": the six steps from a Google Cloud project to a working connection (setup-steps.ts), with links to Google's own help pages.
//
// A <details>: closed or open with the keyboard and announced as a disclosure by screen readers. `open` is only where it STARTS (the tab passes it
// once: open while the package is not set up, closed once it is), so a re-render never closes a guide the person opened, or opens one they closed.
// A link opens in a new tab, says so to a screen reader, and carries no referrer to Google. The service account's address, when the tab knows it,
// is written into the step that grants it access.

import { css, customElement, html, property } from '@umbraco-cms/backoffice/external/lit';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { shared } from '../dashboard.styles';
import { text } from '../localize';
import { GUIDE_STEPS, type GuideLink } from '../setup-steps';

@customElement('esatto-seo-report-setup-guide')
export class EsattoSeoReportSetupGuideElement extends UmbLitElement {
  /** Whether the guide starts open. */
  @property({ type: Boolean }) open = false;

  /** The service account's address, or null while it is not known. */
  @property({ attribute: false }) email: string | null = null;

  override render() {
    return html`
      <details class="card" ?open=${this.open}>
        <summary>
          <span class="icon-badge round" aria-hidden="true"><uui-icon name="icon-help-alt"></uui-icon></span>
          <span class="title">${text(this, 'uiGuideTitle')}</span>
        </summary>
        <div class="body">
          <p>${text(this, 'uiGuideIntroText')}</p>
          <ol>
            ${GUIDE_STEPS.map(
              (step) => html`<li>
                <p>${text(this, step.textKey, step.withEmail ? { email: this.email } : undefined)}</p>
                ${step.links.length > 0 ? html`<p class="links">${step.links.map((link) => this.#link(link))}</p>` : ''}
              </li>`,
            )}
          </ol>
        </div>
      </details>
    `;
  }

  #link(link: GuideLink) {
    return html`<a href=${link.href} target="_blank" rel="noopener noreferrer"
      >${text(this, link.labelKey)}<span class="visually-hidden"> ${text(this, 'uiNewTabText')}</span
      ><uui-icon name="icon-out" aria-hidden="true"></uui-icon
    ></a>`;
  }

  static override styles = [
    shared,
    css`
      summary {
        display: flex; gap: var(--uui-size-space-3); align-items: center; cursor: pointer; list-style: none;
        min-height: 40px; border-radius: var(--seo-radius-sm);
      }
      summary::-webkit-details-marker { display: none; }
      /* The open/closed state in a shape as well as by the panel itself: a chevron that turns. */
      summary::after {
        content: ''; width: 8px; height: 8px; margin-left: auto; border-right: 2px solid currentColor; border-bottom: 2px solid currentColor;
        transform: rotate(45deg); transition: transform var(--seo-ease);
      }
      details[open] > summary::after { transform: rotate(225deg); }
      .title { font-size: 16px; font-weight: 700; }
      .body { display: grid; gap: var(--uui-size-space-4); margin-top: var(--uui-size-space-4); max-width: 80ch; line-height: 1.55; }
      ol { margin: 0; padding-left: var(--uui-size-space-6); display: grid; gap: var(--uui-size-space-4); }
      li { padding-left: var(--uui-size-space-2); }
      .links { display: flex; flex-wrap: wrap; gap: var(--uui-size-space-2) var(--uui-size-space-5); margin-top: var(--uui-size-space-2); font-size: 13px; }
      .links a { display: inline-flex; gap: 4px; align-items: center; }
      .links uui-icon { font-size: 12px; }
    `,
  ];
}

declare global {
  interface HTMLElementTagNameMap {
    'esatto-seo-report-setup-guide': EsattoSeoReportSetupGuideElement;
  }
}
