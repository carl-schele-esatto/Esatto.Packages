// The preferences panel's behaviour (markup: Views/Shared/Components/AccessibilityPanel/Default.cshtml).
// Loaded with `defer`; relies on window.esattoA11y from a11y-boot.js to (re)apply preferences.

import { unobscure } from './obscure';
import { ATTRIBUTES, readPrefs, safeStorage, writePrefs, type PrefKey, type Prefs } from './prefs';

(() => {
  const root = document.querySelector<HTMLElement>('.esatto-a11y');
  const button = root?.querySelector<HTMLButtonElement>('.esatto-a11y__button');
  const dialog = root?.querySelector<HTMLDialogElement>('dialog');
  if (!root || !button || !dialog) return;

  const storage = safeStorage();
  const boxes = Array.from(dialog.querySelectorAll<HTMLInputElement>('input[data-pref]'));
  const apply = (prefs: Prefs) => window.esattoA11y?.apply(prefs);

  /** A box shows what's in effect now, including what follows from the OS. */
  const sync = () => {
    for (const box of boxes) {
      const [name] = ATTRIBUTES[box.dataset.pref as PrefKey];
      box.checked = document.documentElement.hasAttribute(name);
    }
    guide.hidden = !document.documentElement.hasAttribute('data-a11y-guide');
  };

  // Reading guide: a translucent band that follows the pointer or the focused element.
  const guide = document.createElement('div');
  guide.className = 'esatto-a11y-guide';
  guide.setAttribute('aria-hidden', 'true');
  guide.hidden = true;
  document.body.appendChild(guide);
  const moveGuide = (y: number) => (guide.style.transform = `translateY(${Math.max(0, y - guide.offsetHeight / 2)}px)`);
  document.addEventListener('pointermove', (e) => { if (!guide.hidden) moveGuide(e.clientY); }, { passive: true });
  document.addEventListener('focusin', (e) => {
    if (guide.hidden || !(e.target instanceof Element)) return;
    const r = e.target.getBoundingClientRect();
    moveGuide(r.top + r.height / 2);
  });

  // The button floats over the page; it must never hide what has keyboard focus (WCAG 2.4.11). The focused
  // element is scrolled up above it, or at the very end of the page the button fades out until focus moves on.
  document.addEventListener('focusin', (e) => {
    if (!(e.target instanceof Element) || root.contains(e.target)) {
      root.removeAttribute('data-yield');
      return;
    }
    const target = e.target;
    requestAnimationFrame(() => {
      root.removeAttribute('data-yield');
      const scroller = document.scrollingElement ?? document.documentElement;
      const canScrollBy = scroller.scrollHeight - scroller.clientHeight - scroller.scrollTop;
      const action = unobscure(target.getBoundingClientRect(), button.getBoundingClientRect(), canScrollBy);
      if (action === 'yield') {
        root.setAttribute('data-yield', '');
      } else if (action) {
        window.scrollBy({ top: action.scrollBy, behavior: 'instant' });
        // A fixed or sticky element doesn't move when the page scrolls: if it's still covered, step aside.
        if (unobscure(target.getBoundingClientRect(), button.getBoundingClientRect(), 0)) root.setAttribute('data-yield', '');
      }
    });
  });

  button.addEventListener('click', () => {
    sync();
    dialog.showModal(); // focus moves to the first control; Esc closes natively
  });
  dialog.addEventListener('close', () => button.focus());
  // Clicking the backdrop closes too. The dialog element itself is also the target for clicks on its
  // padding, so only a click outside its box counts (a slightly-off click inside must not close it).
  dialog.addEventListener('click', (e) => {
    if (e.target !== dialog) return;
    const r = dialog.getBoundingClientRect();
    const inside = e.clientX >= r.left && e.clientX <= r.right && e.clientY >= r.top && e.clientY <= r.bottom;
    if (!inside) dialog.close();
  });

  for (const box of boxes) {
    box.addEventListener('change', () => {
      const prefs = { ...readPrefs(storage), [box.dataset.pref as PrefKey]: box.checked };
      writePrefs(storage, prefs);
      apply(prefs);
      sync();
    });
  }

  dialog.querySelector('[data-a11y-reset]')?.addEventListener('click', () => {
    writePrefs(storage, {});
    apply({});
    sync();
  });

  sync();
})();
