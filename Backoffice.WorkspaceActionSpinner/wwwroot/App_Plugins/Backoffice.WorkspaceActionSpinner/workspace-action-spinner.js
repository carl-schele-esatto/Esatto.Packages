// Show a spinner on workspace action buttons that gate the actual operation
// behind a confirmation modal (the "Save and publish…" with ellipsis case).
//
// Umbraco's UmbWorkspaceActionElement sets the button state to 'waiting' on
// click, which renders a spinner, but it suppresses that state when the
// action has _additionalOptions === true. The reasoning upstream is "don't
// spin during user interaction in the modal". The gap: after the modal
// closes, 5+ seconds of validate + save + publish still runs server-side
// with no feedback on the button itself — the only indicator is the thin
// progress bar on the tree node, far from where the user clicked.
//
// This shim restores the spinner for the post-modal phase only:
//   1. Capture-phase click listener on <umb-workspace-action>. Also catches
//      clicks on menu items inside the chevron dropdown (publish with
//      descendants, schedule) — their event bubbles through the action.
//   2. After the click, watch the modal-container shadow root for one of
//      the relevant publish/save modals to open and then disconnect.
//   3. After modal disconnect + a short grace, if the action's execute()
//      hasn't resolved (which it would have, immediately, on a cancel),
//      set _buttonState='waiting' + opacity=0.6.
//   4. On the action-executed event (dispatched by both the main button's
//      and menu items' click handlers), clean up. For main-button flows
//      upstream has already set 'success'/'failed' — leave it. For menu-
//      item flows upstream never ran — promote to 'success' and queue a
//      2s reset.
//
// Re-clicks while waiting are blocked at the capture phase. UUI's
// state="waiting" alone doesn't disable the button.

const PATCH_FLAG = '__backofficeWorkspaceActionSpinnerPatched';

if (!window[PATCH_FLAG]) {
  window[PATCH_FLAG] = true;

  const ACTION_TAG = 'UMB-WORKSPACE-ACTION';

  const RELEVANT_MODAL_TAGS = new Set([
    'UMB-DOCUMENT-PUBLISH-MODAL',
    'UMB-DOCUMENT-SAVE-MODAL',
    'UMB-DOCUMENT-PUBLISH-WITH-DESCENDANTS-MODAL',
    'UMB-DOCUMENT-SCHEDULE-MODAL',
  ]);

  // Cancel paths resolve execute() within a few ms after the modal closes;
  // real saves take seconds. This grace lets cancels settle before we
  // decide whether to show the spinner.
  const CANCEL_GRACE_MS = 200;

  const MODAL_OPEN_TIMEOUT_MS = 2000;

  document.addEventListener('click', (event) => {
    const path = event.composedPath();
    const actionEl = path.find((n) => n && n.tagName === ACTION_TAG);
    if (!actionEl) return;
    if (actionEl._buttonState === 'waiting') {
      event.preventDefault();
      event.stopImmediatePropagation();
      return;
    }
    if (!actionEl._additionalOptions) return;
    handleActionClick(actionEl);
  }, true);

  function handleActionClick(actionEl) {
    let executed = false;
    let waitingApplied = false;

    const onExecuted = () => {
      executed = true;
      if (!waitingApplied) return;
      actionEl.style.opacity = '';
      // Main-button flow: upstream #onClick has already set _buttonState to
      // 'success' or 'failed' and queued a reset. Leave it — clearing here
      // would wipe the green checkmark.
      // Menu-item flow (chevron dropdown): upstream #onClick never ran, so
      // _buttonState is still our 'waiting'. Promote to 'success' and
      // queue our own reset so the spinner doesn't get stuck.
      if (actionEl._buttonState === 'waiting') {
        actionEl._buttonState = 'success';
        setTimeout(() => {
          if (actionEl._buttonState === 'success') actionEl._buttonState = undefined;
        }, 2000);
      }
    };
    actionEl.addEventListener('action-executed', onExecuted, { once: true });

    waitForRelevantModalCycle().then((sawModal) => {
      if (!sawModal) {
        actionEl.removeEventListener('action-executed', onExecuted);
        return;
      }
      setTimeout(() => {
        if (executed) {
          actionEl.removeEventListener('action-executed', onExecuted);
          return;
        }
        if (actionEl._buttonState === 'success' || actionEl._buttonState === 'failed') {
          actionEl.removeEventListener('action-executed', onExecuted);
          return;
        }
        actionEl._buttonState = 'waiting';
        actionEl.style.opacity = '0.6';
        blurDeepActiveElement();
        waitingApplied = true;
      }, CANCEL_GRACE_MS);
    });
  }

  let cachedModalContainerShadow = null;

  function getModalContainerShadow() {
    if (cachedModalContainerShadow && cachedModalContainerShadow.host?.isConnected) {
      return cachedModalContainerShadow;
    }
    const container = findElementDeep(document, 'umb-backoffice-modal-container');
    cachedModalContainerShadow = container?.shadowRoot ?? null;
    return cachedModalContainerShadow;
  }

  function findElementDeep(root, tagLower) {
    if (!root) return null;
    const direct = root.querySelector ? root.querySelector(tagLower) : null;
    if (direct) return direct;
    const all = root.querySelectorAll ? root.querySelectorAll('*') : [];
    for (const el of all) {
      if (el.shadowRoot) {
        const inner = findElementDeep(el.shadowRoot, tagLower);
        if (inner) return inner;
      }
    }
    return null;
  }

  function waitForRelevantModalCycle() {
    return new Promise((resolve) => {
      const shadowRoot = getModalContainerShadow();
      if (!shadowRoot) {
        resolve(false);
        return;
      }

      let modalEl = findRelevantModalIn(shadowRoot);
      let openTimeoutId = null;

      const observer = new MutationObserver(() => {
        if (!modalEl) {
          modalEl = findRelevantModalIn(shadowRoot);
          if (modalEl && openTimeoutId !== null) {
            clearTimeout(openTimeoutId);
            openTimeoutId = null;
          }
        } else if (!modalEl.isConnected) {
          observer.disconnect();
          resolve(true);
        }
      });

      observer.observe(shadowRoot, { childList: true, subtree: true });

      if (!modalEl) {
        openTimeoutId = setTimeout(() => {
          observer.disconnect();
          resolve(false);
        }, MODAL_OPEN_TIMEOUT_MS);
      }
    });
  }

  function findRelevantModalIn(root) {
    for (const tag of RELEVANT_MODAL_TAGS) {
      const el = root.querySelector(tag.toLowerCase());
      if (el) return el;
    }
    return null;
  }

  function blurDeepActiveElement() {
    let active = document.activeElement;
    while (active && active.shadowRoot && active.shadowRoot.activeElement) {
      active = active.shadowRoot.activeElement;
    }
    if (active && typeof active.blur === 'function') active.blur();
  }
}
