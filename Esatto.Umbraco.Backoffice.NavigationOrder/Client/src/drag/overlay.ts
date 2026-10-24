// What the user sees while moving tabs: an insertion line, a spinner over the moved tab while saving,
// and an aria-live region for keyboard moves. All fixed-position in <body>, so no shadow root or
// overflow of Umbraco's bars clips them.

const COLOR = 'var(--uui-color-focus, #3879ff)';

export class MoveOverlay {
  #line?: HTMLElement;
  #spinner?: HTMLElement;
  #status?: HTMLElement;

  /** A vertical line at x, as tall as the tab it sits beside. */
  showLine(x: number, top: number, height: number): void {
    const line = (this.#line ??= this.#mount('div', `position:fixed;z-index:10000;width:3px;border-radius:2px;pointer-events:none;background:${COLOR};`));
    Object.assign(line.style, { left: `${Math.round(x - 1.5)}px`, top: `${Math.round(top + 6)}px`, height: `${Math.max(0, Math.round(height - 12))}px`, display: 'block' });
  }

  hideLine(): void {
    if (this.#line) this.#line.style.display = 'none';
  }

  /** A small spinner at the tab's top-right corner, labelled for screen readers. */
  showSpinner(rect: DOMRect, label: string): void {
    const spinner = (this.#spinner ??= this.#mount('uui-loader-circle', `position:fixed;z-index:10000;font-size:12px;pointer-events:none;color:${COLOR};`));
    spinner.setAttribute('aria-label', label);
    Object.assign(spinner.style, { left: `${Math.round(rect.right - 14)}px`, top: `${Math.round(rect.top + 4)}px`, display: 'block' });
  }

  hideSpinner(): void {
    if (this.#spinner) this.#spinner.style.display = 'none';
  }

  /** Read out by screen readers (polite). */
  announce(message: string): void {
    const status = (this.#status ??= this.#mount(
      'div',
      'position:fixed;width:1px;height:1px;margin:-1px;padding:0;overflow:hidden;clip:rect(0 0 0 0);white-space:nowrap;border:0;',
    ));
    status.setAttribute('role', 'status');
    status.setAttribute('aria-live', 'polite');
    status.textContent = '';
    // A fresh text node after clearing makes repeated, identical messages be read again.
    requestAnimationFrame(() => (status.textContent = message));
  }

  #mount(tag: string, css: string): HTMLElement {
    const element = document.createElement(tag);
    element.style.cssText = css;
    document.body.appendChild(element);
    return element;
  }
}
