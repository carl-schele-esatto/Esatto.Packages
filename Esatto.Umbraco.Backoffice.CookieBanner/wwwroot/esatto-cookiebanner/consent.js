/**
 * Esatto cookie consent.
 *
 * Deliberately dependency-free and self-hosted: a consent tool that itself calls out to a third
 * party would undercut its own purpose.
 *
 * The server is the source of truth. This script never writes the consent cookie - it posts the
 * choice and lets the endpoint set it, which is what guarantees the cookie's attributes are right.
 */
(function () {
    'use strict';

    // consent.js can now be referenced by more than one <script> tag on the same page: the
    // <consent-banner /> tag helper's script tag, and the one Views/CookiePolicy.cshtml renders for
    // itself so its own data-consent-* buttons work even when the layout hosting the policy page
    // omits <consent-banner />. Two <script src> tags pointing at the same URL are fetched once by
    // the browser, but each tag is still its own script inclusion and runs the whole file again, so
    // without this guard the document-level click listener and everything else below would be
    // registered twice, double-firing every click. Bail out on the second run.
    if (window.cookieConsent) { return; }

    var script = document.currentScript;
    var endpoint = script.getAttribute('data-consent-endpoint') || '/api/cookie-consent';
    var cookieName = script.getAttribute('data-consent-cookie') || 'cookie-consent';
    var policyVersion = parseInt(script.getAttribute('data-consent-version') || '1', 10);
    var consentModeEnabled = script.getAttribute('data-consent-mode') === 'on';
    var needsDecision = script.getAttribute('data-consent-needs-decision') === 'true';
    var errorMessage = script.getAttribute('data-consent-error-message') || 'Something went wrong. Please try again.';
    var rateLimitedMessage = script.getAttribute('data-consent-rate-limited-message')
        || 'You have tried too many times. Please wait a moment and try again.';

    // Set only when open() finds the dialog cannot actually be displayed. Stops the 'close'
    // handler from reopening an invisible modal in a loop.
    var blockingAbandoned = false;

    // Armed by open(); consumed by the first focus change after it. See the 'focusin' handler.
    var reclaimFocus = false;

    var listeners = [];

    function readCookie() {
        var prefix = cookieName + '=';
        var parts = document.cookie ? document.cookie.split('; ') : [];

        for (var i = 0; i < parts.length; i++) {
            if (parts[i].indexOf(prefix) !== 0) { continue; }
            try {
                var parsed = JSON.parse(decodeURIComponent(parts[i].substring(prefix.length)));
                if (!parsed || typeof parsed.v !== 'number') { return null; }
                return {
                    version: parsed.v,
                    decidedAt: parsed.t,
                    categories: Array.isArray(parsed.c) ? parsed.c : [],
                    consentId: parsed.id
                };
            } catch (error) {
                return null;
            }
        }

        return null;
    }

    function currentCategories() {
        var state = readCookie();
        if (!state || state.version < policyVersion) { return []; }
        return state.categories;
    }

    function has(category) {
        return category === 'necessary' || currentCategories().indexOf(category) !== -1;
    }

    var dialog = document.getElementById('esatto-consent-dialog');

    /**
     * True once the dialog actually occupies space in the layout - not merely `open`. Guards
     * against a zero-height dialog (a stylesheet conflict, or one stripped by a browser
     * extension) leaving the visitor stuck behind a dimmed, unusable page.
     */
    function isDisplayed(element) {
        var box = element.getBoundingClientRect();
        return box.width > 0 && box.height > 0;
    }

    /**
     * Put focus on the dialog's heading. The heading carries tabindex="-1" and is not interactive,
     * so focus starts inside the dialog - keeping the focus trap and screen-reader announcement -
     * without any control appearing pre-selected.
     */
    function focusHeading() {
        if (!dialog || dialog.open === false) { return; }

        var heading = dialog.querySelector('#esatto-consent-dialog-heading');
        if (!heading || typeof heading.focus !== 'function') { return; }

        // preventScroll matters: the dialog scrolls internally, and focusing an element scrolls it
        // into view by default. Without this, pressing Escape part-way down the cookie list yanked
        // the dialog back to the top. Older browsers ignore the options object and simply scroll,
        // which is the pre-existing behaviour rather than a new failure.
        heading.focus({ preventScroll: true });
    }

    /**
     * First run renders Accept all and Reject all only, plus the control that reveals per-category
     * choice. Revealing swaps that control for Save and moves focus into the section it opened: the
     * control the visitor just activated is about to be hidden, and focus left on a hidden element
     * falls out of the modal entirely.
     */
    function revealCategories(trigger) {
        if (!dialog) { return; }

        var categories = dialog.querySelector('[data-consent-categories]');
        var save = dialog.querySelector('[data-consent-action="custom"]');

        if (categories) { categories.hidden = false; }
        if (save) { save.hidden = false; }
        if (trigger) { trigger.hidden = true; }

        var firstInput = categories
            && categories.querySelector('[data-consent-category-input]:not([disabled])');

        if (firstInput && typeof firstInput.focus === 'function') {
            firstInput.focus({ preventScroll: true });
        } else {
            focusHeading();
        }
    }

    /**
     * @param {boolean} isReopen True only when reopening because the dialog was closed with no
     *   decision recorded. The focus reclaim is armed for that case ONLY: on a first open there is
     *   no restoration to fight, and arming it would steal the visitor's first deliberate click.
     */
    function open(isReopen) {
        if (!dialog) { return; }

        var dialogSupported = typeof HTMLDialogElement === 'function'
            && typeof dialog.showModal === 'function';

        if (!dialogSupported) {
            // No native modal <dialog> support: still offer the choice, just not modally -
            // an unusable site is worse than a non-blocking one.
            if (window.console) {
                console.warn('cookie-consent: dialog.showModal is not supported; showing the cookie choice without blocking the page.');
            }
            dialog.setAttribute('open', 'open');
            return;
        }

        dialog.showModal();

        // Closing a <dialog> restores focus to whatever was focused before it opened, and that can
        // land after this handler has run - stealing focus back to the control the visitor had
        // clicked, which then shows a focus ring on it. Racing it with a timer is unreliable, so on
        // a reopen arm a one-shot reclaim instead: focus the heading, then take focus back from
        // whatever grabs it next. Ordering-independent, and it disarms immediately.
        focusHeading();
        if (isReopen === true) { reclaimFocus = true; }

        if (!isDisplayed(dialog)) {
            // showModal() ran but the dialog is not actually visible (a CSS conflict, a browser
            // extension removed it, etc.). A dimmed, invisible modal traps the visitor worse
            // than no consent UI at all, so fail open instead.
            if (window.console) {
                console.warn('cookie-consent: the consent dialog could not be displayed; leaving the page usable.');
            }
            blockingAbandoned = true;
            dialog.close();
        }
    }

    function close() {
        if (!dialog) { return; }
        if (typeof dialog.close === 'function') {
            dialog.close();
        } else {
            dialog.removeAttribute('open');
        }
    }

    // While no decision has been made yet, there is nothing to cancel back to, so Escape must not
    // dismiss the choice. Two layers are needed, because one is not enough:
    //
    // 1. preventDefault() on 'cancel'. This works once the visitor has interacted with the page,
    //    but browsers deliberately ignore it for a dialog opened WITHOUT user activation - which is
    //    exactly our case, since the dialog opens on load. That is anti-abuse behaviour by design
    //    (a page must not be able to trap you), so it cannot be argued with.
    // 2. Reopen on 'close' whenever no decision has been recorded. That covers the first Escape,
    //    which layer 1 cannot. After it, user activation exists and layer 1 handles the rest.
    if (dialog) {
        dialog.addEventListener('cancel', function (event) {
            if (needsDecision === false) { return; }

            event.preventDefault();

            // Escape was swallowed, so the dialog stays put - but the keypress has switched the
            // browser into keyboard modality, which makes :focus-visible start matching whatever
            // already had focus. A control the visitor merely clicked (focused without a ring)
            // suddenly grows one, reading as though Escape had selected it. Moving focus to the
            // non-interactive heading leaves nothing for a ring to be drawn on.
            focusHeading();
        });

        dialog.addEventListener('close', function () {
            // blockingAbandoned means open() already determined the dialog cannot be displayed and
            // closed it on purpose. Reopening then would loop forever on an invisible modal.
            if (needsDecision && blockingAbandoned === false) { open(true); }
        });

        // The one-shot reclaim armed by open(). Fires for the browser's post-close focus
        // restoration and hands focus back to the heading, then disarms.
        dialog.addEventListener('focusin', function (event) {
            if (reclaimFocus === false) { return; }

            reclaimFocus = false;
            if (event.target === dialog.querySelector('#esatto-consent-dialog-heading')) { return; }

            // Blur first: that clears the ring the browser has already drawn on the control,
            // rather than leaving it painted while focus moves elsewhere.
            if (event.target && typeof event.target.blur === 'function') { event.target.blur(); }
            focusHeading();
        });
    }

    function updateConsentMode() {
        if (!consentModeEnabled || typeof window.gtag !== 'function') { return; }

        var marketing = has('marketing') ? 'granted' : 'denied';

        window.gtag('consent', 'update', {
            ad_storage: marketing,
            ad_user_data: marketing,
            ad_personalization: marketing,
            analytics_storage: has('statistics') ? 'granted' : 'denied',
            functionality_storage: has('preferences') ? 'granted' : 'denied',
            personalization_storage: has('preferences') ? 'granted' : 'denied'
        });
    }

    function announce() {
        var detail = { categories: currentCategories(), version: policyVersion };

        document.dispatchEvent(new CustomEvent('cookieconsent:change', { detail: detail }));
        listeners.forEach(function (listener) {
            try { listener(detail); } catch (error) { /* a bad subscriber must not break consent */ }
        });
    }

    function selectedCategories() {
        var inputs = document.querySelectorAll('[data-consent-category-input]');

        return Array.prototype.filter.call(inputs, function (input) {
            return input.checked && !input.disabled;
        }).map(function (input) {
            return input.value;
        });
    }

    function statusElements() {
        return document.querySelectorAll('[data-consent-status]');
    }

    function actionButtons() {
        return document.querySelectorAll('[data-consent-action]');
    }

    /** role="status"/aria-live elements, so screen reader users hear a failure too, not just see it. */
    function showStatus(message) {
        Array.prototype.forEach.call(statusElements(), function (element) {
            element.textContent = message;
            element.hidden = false;
        });
    }

    function clearStatus() {
        Array.prototype.forEach.call(statusElements(), function (element) {
            element.textContent = '';
            element.hidden = true;
        });
    }

    /** Prevents a double-click (or a slow request plus an impatient second click) from firing twice. */
    function setActionButtonsDisabled(disabled) {
        Array.prototype.forEach.call(actionButtons(), function (button) {
            button.disabled = disabled;
        });
    }

    function send(action, categories) {
        return fetch(endpoint, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            credentials: 'same-origin',
            body: JSON.stringify({
                categories: categories,
                action: action
            })
        }).then(function (response) {
            if (!response.ok) {
                var error = new Error('Consent request failed: ' + response.status);
                error.status = response.status;
                throw error;
            }
            return response.json();
        }).then(function () {
            // A decision now demonstrably exists (the server accepted it and set the cookie):
            // Escape and the cancel affordance behave normally on any future reopen this page.
            // The Cancel button itself is server-rendered and stays absent until the next
            // navigation - only Escape-suppression needs to be lifted here.
            //
            // This must be cleared BEFORE close(), because the 'close' handler reopens the dialog
            // whenever it closes with no decision recorded. Closing first would bounce it straight
            // back open on the success path.
            needsDecision = false;

            close();
            clearStatus();
            updateConsentMode();
            announce();

            // Reload rather than patch the page in place, for EVERY action, not only withdrawal:
            // <consent-script> and <consent-embed> gate their output server-side and simply
            // suppress it while consent is absent, so nothing on the current page becomes live
            // just because the cookie changed - granting consent activated nothing here, the
            // landing page's own analytics pageview was lost, and the dialog's checkboxes kept
            // showing the pre-decision state, so reopening it and pressing Save again silently
            // re-saved (and could revoke) what had just been granted. A reload re-renders every
            // server-driven bit of that state from scratch, so the tag helpers, the dialog and the
            // consent cookie all agree the moment the page comes back.
            window.location.reload();
            return true;
        }).catch(function (error) {
            // Leave the dialog in place: a failed request must not read as a recorded choice.
            if (window.console) { console.error(error); }
            showStatus(error && error.status === 429 ? rateLimitedMessage : errorMessage);
            return false;
        });
    }

    function decide(action) {
        clearStatus();
        setActionButtonsDisabled(true);

        // Every branch reloads on success now (see send()'s success handler) and leaves the page
        // untouched on failure (`send` resolves false, never rejects), so there is nothing left to
        // special-case here: withdrawn used to be the only action that reloaded.
        var result;
        if (action === 'accept-all') { result = send(action, ['preferences', 'statistics', 'marketing']); }
        else if (action === 'reject-all') { result = send(action, []); }
        else if (action === 'withdrawn') { result = send(action, []); }
        else { result = send('custom', selectedCategories()); }

        return result.then(function (succeeded) {
            setActionButtonsDisabled(false);
            return succeeded;
        });
    }

    document.addEventListener('click', function (event) {
        var target = event.target;
        // This handler lives at the document level for the life of the page, so guard against
        // any click target that is not an Element (e.g. a Text node reached via composed paths).
        if (!target || typeof target.closest !== 'function') { return; }

        var opener = target.closest('[data-consent-open]');
        if (opener) { event.preventDefault(); open(); return; }

        var customiser = target.closest('[data-consent-customise]');
        if (customiser) { event.preventDefault(); revealCategories(customiser); return; }

        var closer = target.closest('[data-consent-close]');
        if (closer) { event.preventDefault(); close(); return; }

        var actor = target.closest('[data-consent-action]');
        if (actor) { event.preventDefault(); decide(actor.getAttribute('data-consent-action')); }
    });

    // Anything already granted from a previous visit is already live: <consent-script> and
    // <consent-embed> render their real output server-side whenever consent.HasGranted() is true,
    // so there is nothing left to activate client-side on load - only Consent Mode signalling.
    updateConsentMode();

    // No decision yet: block the site until one is made.
    if (needsDecision) { open(); }

    window.cookieConsent = {
        open: open,
        close: close,
        get: readCookie,
        has: has,
        onChange: function (fn) { if (typeof fn === 'function') { listeners.push(fn); } }
    };
})();
