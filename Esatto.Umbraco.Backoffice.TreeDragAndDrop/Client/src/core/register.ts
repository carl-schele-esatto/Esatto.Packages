// registerTree(): the only thing an entry module calls. Idempotent — a second
// call for the same tree (entry re-evaluated) or another tree only adds the
// adapter; the observer patch and host element are installed once.

import { registerAdapter } from './adapters';
import { installObserver, resweep } from './observer';
import { EsattoTreeDnd } from './host';
import { HOST_TAG } from './constants';
import type { TreeAdapter } from './types';

export function registerTree(adapter: TreeAdapter): void {
  registerAdapter(adapter);
  installObserver();
  mountHost();
  // Rows of this tree may already be rendered and swept before its adapter
  // existed (isTreeItem didn't match them then). Sweep again.
  resweep();
}

function mountHost(): void {
  if (!customElements.get(HOST_TAG)) customElements.define(HOST_TAG, EsattoTreeDnd);
  if (document.querySelector(HOST_TAG)) return;

  // Mount the host INSIDE <umb-app> so Lit contexts (UMB_NOTIFICATION_CONTEXT,
  // UMB_ACTION_EVENT_CONTEXT) provided by ancestors of the section roots can
  // propagate down to it. Mounting on document.body as a sibling of umb-app
  // means consumeContext silently fails — the action-event context (needed to
  // trigger tree reloads after a move) never resolves.
  const host = document.createElement(HOST_TAG);
  host.style.display = 'none';
  const umbApp = document.querySelector('umb-app');
  if (umbApp) {
    umbApp.appendChild(host);
    return;
  }

  // umb-app not yet in the DOM — start mounted on body so the sweep/observe
  // machinery is live, and re-parent into umb-app when it shows up. The
  // disconnect/reconnect on re-parent re-runs setHost → re-sweep.
  document.body.appendChild(host);
  const obs = new MutationObserver(() => {
    const ua = document.querySelector('umb-app');
    if (ua) {
      ua.appendChild(host);
      obs.disconnect();
    }
  });
  obs.observe(document.body, { childList: true });
}
