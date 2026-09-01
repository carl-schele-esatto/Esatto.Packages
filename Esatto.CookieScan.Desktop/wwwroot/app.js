/*
    The shell: the router, and the bridge to the window that hosts it.

    Hash routing, never the history API: the page is served from a virtual origin by the window's own
    request handler, so a pushState route would 404 the moment anything reloaded it. `#scan` is not a
    cosmetic choice - it is the only kind of URL this document can survive.
*/

/*
    Imported by name rather than for its side effect alone. The element registers itself when this
    module loads either way, but the asset test crawls rooted import specifiers, and a side-effect
    import names no binding for it to see - which would leave a renamed component as a blank panel
    with a 404 nobody notices, the exact failure that test exists to catch.

    Keep example import syntax out of these comments, too: the crawl is a regular expression over the
    file's text, and it cannot tell a specifier in a comment from one the module really imports.
*/
import { DiffView } from '/components/cs-diff-view.js';
import { FindingsTable } from '/components/cs-findings-table.js';
import { HistoryList } from '/components/cs-history-list.js';
import { LogPanel } from '/components/cs-log-panel.js';
import { StatTile } from '/components/cs-stat-tile.js';
import { TrendChart } from '/components/cs-trend-chart.js';
import { DEFAULT_LOG_THEME, LOG_THEMES, logTheme } from '/log-themes.js';

const FALLBACK = 'scan';

const links = Array.from(document.querySelectorAll('.nav-link[data-page]'));
const pages = Array.from(document.querySelectorAll('.page[data-page]'));

let routed = false;

/** The page the current hash names, or the fallback if it names nothing this shell knows. */
function requestedPage() {
  const name = location.hash.replace(/^#/, '');

  return pages.some((page) => page.dataset.page === name) ? name : FALLBACK;
}

function show(name) {
  let shown = null;

  for (const page of pages) {
    const active = page.dataset.page === name;

    page.hidden = !active;

    if (active) {
      shown = page;
    }
  }

  for (const link of links) {
    if (link.dataset.page === name) {
      link.setAttribute('aria-current', 'page');
    } else {
      // Removed rather than set to "false": aria-current="false" is still an announced value.
      link.removeAttribute('aria-current');
    }
  }

  if (shown === null) {
    return;
  }

  // Not on the first route: focusing the heading as the window opens would be a jump nobody asked
  // for. On every later change it is the only signal a keyboard reader gets that the page moved.
  if (routed) {
    shown.focus();
  }

  routed = true;

  // Composed and bubbling so a component inside a shadow root can hear it on `document` too. The
  // pages listen for this instead of polling, because a page that is never opened should not work.
  shown.dispatchEvent(new CustomEvent('page-shown', {
    detail: { page: name },
    bubbles: true,
    composed: true,
  }));
}

function route() {
  const name = requestedPage();

  // An empty or unknown hash is rewritten so the address and the page never disagree. replace()
  // rather than assignment: a corrected hash is not somewhere the user should be able to go back to.
  // This fires hashchange, which re-enters here once with the hash already correct.
  if (location.hash !== `#${name}`) {
    location.replace(`#${name}`);

    return;
  }

  show(name);
}

window.addEventListener('hashchange', route);

route();

/* ---------------------------------------------------------------- the host bridge

    One JSON envelope in each direction: `window.chrome.webview.postMessage` out, the `message` event
    in. Every later page adds message types here and nothing else - there is one transport, and this
    is it.

    `chrome.webview` exists only inside the window that hosts this page, so every use of it is
    optional-chained: opened in an ordinary browser the page still renders, it simply has nobody to
    talk to.
*/

const host = window.chrome?.webview;

const scanPage = pages.find((page) => page.dataset.page === 'scan');

const brandSite = document.querySelector('#brand-site');
const form = document.querySelector('#scan-form');
const siteSelect = document.querySelector('#scan-site');
const urlInput = document.querySelector('#scan-url');
const maxPagesInput = document.querySelector('#scan-max-pages');
const localeInput = document.querySelector('#scan-locale');
const memberEmailInput = document.querySelector('#scan-member-email');
const memberPasswordInput = document.querySelector('#scan-member-password');
const clientIdInput = document.querySelector('#scan-client-id');
const clientSecretInput = document.querySelector('#scan-client-secret');
const consentCookieInput = document.querySelector('#scan-consent-cookie');
const dryRunInput = document.querySelector('#scan-dry-run');
/** Every masked box's reveal button, on both pages - see setRevealed. */
const revealButtons = Array.from(document.querySelectorAll('.reveal'));
const runButton = document.querySelector('#scan-run');
const cancelButton = document.querySelector('#scan-cancel');
const saveSiteButton = document.querySelector('#scan-save-site');
const deleteSiteButton = document.querySelector('#scan-delete-site');
const optionsDetails = document.querySelector('#scan-options');
const secretStatus = document.querySelector('#secret-status');

// The site's own email settings: who hears about a scan of THIS site. The server they leave through
// is one per machine and lives on the Email page below.
const emailEnabledInput = document.querySelector('#scan-email-enabled');
const emailFields = document.querySelector('#scan-email-fields');
const emailToInput = document.querySelector('#scan-email-to');
const emailCcInput = document.querySelector('#scan-email-cc');
const emailRecipientsNote = document.querySelector('#email-recipients-note');

// The two manual send controls - the result card's and the History detail's - and the Email page's
// own test row. All three are the same shape: a button that reveals a row with an address in it,
// because a send cannot be undone and a button that mailed a client the instant it was pressed
// would hide the one thing worth checking first.
const scanEmailSendButton = document.querySelector('#scan-email-send');
const scanEmailBar = document.querySelector('#scan-email-bar');
const scanEmailManualTo = document.querySelector('#scan-email-manual-to');
const historyEmailSendButton = document.querySelector('#history-email-send');
const historyEmailBar = document.querySelector('#history-email-bar');
const historyEmailManualTo = document.querySelector('#history-email-manual-to');
const historyEmailToast = document.querySelector('#history-email-toast');

const emailForm = document.querySelector('#email-form');
const emailHostInput = document.querySelector('#email-host');
const emailPortInput = document.querySelector('#email-port');
const emailSecurityInput = document.querySelector('#email-security');
const emailUsernameInput = document.querySelector('#email-username');
const emailPasswordInput = document.querySelector('#email-password');
const emailFromAddressInput = document.querySelector('#email-from-address');
const emailFromNameInput = document.querySelector('#email-from-name');
const emailTestButton = document.querySelector('#email-test');
const emailTestBar = document.querySelector('#email-test-bar');
const emailTestToInput = document.querySelector('#email-test-to');
const emailTestSendButton = document.querySelector('#email-test-send');
const emailToast = document.querySelector('#email-toast');

/**
 * What the host said on ready about the client secret FALLBACK - whether the machine's variable is
 * set, and what it is called. Not the secret itself and not the secret a scan will use: the secret
 * lives in the profile now, and this is only what a run started with an empty box would fall back
 * on. Kept so the note under the credential pair can be recomputed whenever either box changes.
 * @type {{ isSet: boolean, variable: string } | null}
 */
let secret = null;

const siteToast = document.querySelector('#site-toast');

/**
 * What the page last asked the host to do to a profile, so the `sites` answer can be turned into
 * words. The answer itself carries only the list: a saved site and a deleted one both arrive as
 * "here is the list now", and the page is the one that knows which it asked for.
 * @type {{ verb: string, url: string } | null}
 */
let pendingSiteAction = null;

let toastTimer = 0;

/** Shows one line beside the buttons for a few seconds, then fades it out. */
function showToast(text) {
  clearTimeout(toastTimer);

  siteToast.textContent = text;
  siteToast.hidden = false;
  siteToast.classList.remove('is-fading');

  toastTimer = setTimeout(() => {
    siteToast.classList.add('is-fading');

    // Hidden only after the fade has had its 400ms, so the region empties rather than blinking out.
    toastTimer = setTimeout(() => {
      siteToast.hidden = true;
      siteToast.textContent = '';
    }, 450);
  }, 4000);
}

/**
 * The line under the credential pair: what is missing, if anything, from the two boxes plus the
 * machine's fallback.
 *
 * Silent whenever the pair can actually sign in - an id with a secret beside it, or an id with the
 * environment variable behind it. The dots in the boxes are already the signal that something is
 * filled in, and a line repeating that was noise.
 *
 * When it does speak, it names the ONE thing that is missing, which is why the empty-id branch is
 * three answers rather than one: an operator who has typed a secret and no id has a different next
 * move from one who has typed neither, and telling the first of them "no API credentials" would be
 * false as well as useless. A typed secret is checked before the fallback for the same reason - the
 * fallback is irrelevant to someone who has just typed a secret of their own.
 *
 * Muted in every state, and phrased as a fact rather than an error. Report-only is a supported mode:
 * a scan with no credentials at all still finds every cookie, it just does not write the policy page.
 */
function showSecretStatus() {
  // Before `ready`: nothing is known about the fallback yet, so any line here would be a guess -
  // and the one guess available ("no fallback") is the alarming one.
  if (secret === null) {
    secretStatus.hidden = true;

    return;
  }

  const idGiven = clientIdInput.value.trim().length > 0;
  const secretGiven = clientSecretInput.value.trim().length > 0;

  // Read through the box rather than through the profile, because the box is what a run posts: a
  // saved secret the operator has just cleared is not a secret this scan will use.
  if (idGiven && (secretGiven || secret.isSet)) {
    secretStatus.hidden = true;
    secretStatus.textContent = '';

    return;
  }

  secretStatus.hidden = false;

  if (idGiven) {
    secretStatus.textContent = 'No client secret - write-back will be skipped';

    return;
  }

  if (secretGiven) {
    secretStatus.textContent = 'A client id is needed with the secret';

    return;
  }

  secretStatus.textContent = secret.isSet
    ? `${secret.variable} is set - a client id completes the pair`
    : 'No API credentials - the scan runs report-only';
}

/** @type {LogPanel} */
const logPanel = document.querySelector('#scan-log');
const logThemeSelect = document.querySelector('#scan-log-theme');

// The log's colour scheme: three custom properties set on the panel itself, which is where its
// styles read them, so the choice reaches the placeholder and the warning tint as well as the lines.
// Remembered in localStorage rather than in the profiles file - a preference about this screen on
// this machine, not a fact about a site - and the page has a real origin to keep it under.
const LOG_THEME_KEY = 'logTheme';

function applyLogTheme(id) {
  const theme = logTheme(id);

  logPanel.style.setProperty('--log-bg', theme.bg);
  logPanel.style.setProperty('--log-ink', theme.ink);
  logPanel.style.setProperty('--log-warn', theme.warn);
  logThemeSelect.value = theme.id;
}

function rememberedLogTheme() {
  // Storage can be absent or refused; the default is the answer either way.
  try {
    return localStorage.getItem(LOG_THEME_KEY) ?? DEFAULT_LOG_THEME;
  } catch {
    return DEFAULT_LOG_THEME;
  }
}

for (const theme of LOG_THEMES) {
  const option = document.createElement('option');

  option.value = theme.id;
  option.textContent = theme.name;
  logThemeSelect.append(option);
}

applyLogTheme(rememberedLogTheme());

logThemeSelect.addEventListener('change', () => {
  applyLogTheme(logThemeSelect.value);

  try {
    localStorage.setItem(LOG_THEME_KEY, logThemeSelect.value);
  } catch {
    // Not remembered, still applied: the operator keeps the colours they picked for this session.
  }
});

const findings = document.querySelector('#scan-findings');

/** @type {FindingsTable} */
const findingsTable = document.querySelector('#scan-findings-table');

const trendCard = document.querySelector('#trend-card');

/** @type {TrendChart} */
const trendChart = document.querySelector('#scan-trend');

/** @type {Record<string, StatTile>} */
const tiles = {
  entries: document.querySelector('#tile-entries'),
  violations: document.querySelector('#tile-violations'),
  review: document.querySelector('#tile-review'),
  expected: document.querySelector('#tile-expected'),
};

/** @type {HistoryList} */
const historyList = document.querySelector('#history-list');
const historyClearButton = document.querySelector('#history-clear');
const historyUnselectButton = document.querySelector('#history-unselect');

const historyDetail = document.querySelector('#history-detail');
const historyError = document.querySelector('#history-error');

/** @type {FindingsTable} */
const historyFindingsTable = document.querySelector('#history-findings-table');

const historyDiff = document.querySelector('#history-diff');
const historyDiffError = document.querySelector('#history-diff-error');

/** @type {DiffView} */
const historyDiffView = document.querySelector('#history-diff-view');

const lastScanValue = document.querySelector('#last-scan');
const keptScansValue = document.querySelector('#kept-scans');
const appVersionValue = document.querySelector('#app-version');

/**
 * Everything a running scan must not let the operator change under it.
 *
 * The site dropdown is in here for a reason beyond tidiness: choosing a profile REFILLS the form,
 * so leaving it live during a run would let the fields the scan is reporting on be replaced under
 * it. Save site and Delete are deliberately absent - their disabled state has a second condition
 * each, so syncSiteButtons owns it rather than sharing it with this list.
 */
const inputs = [
  siteSelect, urlInput, maxPagesInput, localeInput, memberEmailInput,
  memberPasswordInput, clientIdInput, clientSecretInput, dryRunInput, runButton,
  // The email trio is part of the profile the run writes, so it locks with the rest of it: a
  // recipient typed halfway through a scan would be saved by that scan without ever having been on
  // screen when it started.
  emailEnabledInput, emailToInput, emailCcInput,
];

/** Whether a scan is running, as the host last reported it. */
let running = false;

/** Every saved profile, as the host last reported it, in the order the dropdown shows them. */
let sites = [];

/**
 * What the dry-run box shows, per site URL, for the rest of this session - including a tick the
 * operator never saved.
 *
 * The one field in the form that survives a site switch. Every other box is refilled from the
 * profile, discarding unsaved edits, and dry run was too until that proved wrong at the chair:
 * turning it off, glancing at another site and coming back found it back on, so the box could not
 * be trusted to still say what it had been left saying.
 *
 * A URL absent from this map shows a checked box, never the profile's saved value - see fillDryRun
 * for why the stored setting is not what a fresh session should honour.
 */
const dryRunByUrl = new Map();

/**
 * What "New site" means: the fields a profile that does not exist yet would have.
 *
 * The same defaults the window has always opened with - 25 pages, Swedish. Kept here rather than as
 * `value` attributes in the markup because this is also what Delete and a failed lookup fall back
 * to, and three places reading a form's initial state out of the DOM would drift.
 *
 * No `dryRun`: no profile's stored setting reaches the box, this one included, so a value here
 * would be one nothing reads. That default lives in fillDryRun, which is the only thing that
 * decides it.
 */
const NEW_SITE = {
  url: '',
  maxPages: 25,
  locale: 'Sv',
  memberEmail: '',
  memberPassword: '',
  clientId: '',
  clientSecret: '',
  // Blank means the CookieBannerOptions default, which is what the field's own placeholder shows.
  consentCookie: '',
  // Off, and empty. A new site must not inherit the last one's recipients: mailing a client's
  // cookie report to a different client is the one mistake here that cannot be taken back.
  emailEnabled: false,
  emailTo: '',
  emailCc: '',
};

/** Every kept scan the host has told us about, newest first, for every site. */
let history = [];

/**
 * ScanHistory.Keep, mirrored here rather than sent on the envelope: the count the sidebar reads is
 * the length of the list the host already sent, and this is only the denominator "N of 50" needs.
 */
const HISTORY_KEEP = 50;

function post(message) {
  host?.postMessage(message);
}

function setRunning(next) {
  running = next;

  for (const input of inputs) {
    input.disabled = next;
  }

  // Swapped rather than both shown: there is exactly one thing to do at any moment, and a disabled
  // Cancel sitting beside a disabled Run says nothing about which.
  runButton.hidden = next;
  cancelButton.hidden = !next;
  cancelButton.disabled = false;

  // After the loop above and not in it: these two answer to more than `running`.
  syncSiteButtons();

  setStale(next);
}

/**
 * Whether Save site and Delete can do anything right now.
 *
 * Save needs a URL, because the URL is the profile's identity and a nameless profile is not a
 * thing the file can hold. Delete needs a profile selected, because "New site" is the absence of
 * one. Both need the scan to be over, for the same reason every field above does.
 *
 * Disabled rather than hidden, unlike Run and Cancel: those two are one action in two states, and
 * these are two actions that are momentarily unavailable. A row of buttons that changed length as
 * the form was typed into would be worse than a greyed one.
 */
function syncSiteButtons() {
  saveSiteButton.disabled = running || urlInput.value.trim() === '';
  deleteSiteButton.disabled = running || siteSelect.value === '';
}

/**
 * De-emphasises the previous scan's numbers while the next scan runs.
 *
 * They stay on screen: the run you are about to compare against is exactly the one that pressing
 * Run would otherwise throw away, and a scan takes the best part of a minute. Dimmed rather than
 * left at full strength, because tiles and a chart that still read as current would be claiming to
 * describe the run in progress.
 *
 * aria-busy carries the same thing to a reader who gets nothing from opacity - it is the only cue
 * there is otherwise, and this window does not convey anything by appearance alone.
 */
function setStale(stale) {
  for (const region of [trendCard, findings]) {
    region.classList.toggle('is-stale', stale);

    if (stale) {
      region.setAttribute('aria-busy', 'true');
    } else {
      // Removed rather than set to "false": an element that is not busy should say nothing about
      // it, the same reasoning as aria-current on the navigation.
      region.removeAttribute('aria-busy');
    }
  }
}

/**
 * The site the URL field names, in a form two spellings of the same site agree on.
 *
 * The history records a site as the scanned Uri's own text - "https://localhost:44351/", with the
 * trailing slash Uri adds - while the field holds whatever was typed. Comparing them raw would
 * leave the chart permanently empty for the site sitting in the box.
 */
function siteKey(site) {
  return typeof site === 'string' ? site.trim().toLowerCase().replace(/\/+$/, '') : '';
}

/**
 * Hands the chart the scans for the site currently in the URL field.
 *
 * The filter lives here rather than in the component because the URL field is this module's, and a
 * chart that reached into the form for it would be a second thing that has to know where the field
 * is. What the component decides is how much of what it is given it can draw.
 */
function showTrend() {
  const wanted = siteKey(urlInput.value);

  trendChart.entries = wanted === ''
    ? []
    : history.filter((entry) => siteKey(entry?.site) === wanted);
}

/**
 * Puts the host of the site in the URL field under the sidebar's title.
 *
 * The label used to be the one site this tool was built inside. It is now a package any Esatto site
 * can point at, so a fixed name there would be wrong for every site but one - and the operator's
 * real question, with two windows open onto staging and production, is which of them this one is.
 * Falls back to the product name rather than to an empty line, so the sidebar never looks broken
 * while the field is empty or holds a half-typed address.
 */
function showBrandSite() {
  let label = 'Esatto';

  try {
    const { host } = new URL(urlInput.value.trim());

    if (host !== '') {
      label = host;
    }
  } catch {
    // A half-typed address is the normal state of that field, not a fault. Keep the fallback.
  }

  brandSite.textContent = label;
}

/**
 * Fills the sidebar's two footer values from the same `history` message the trend and the History
 * page both read from: the last scan's own time and entry count, and how many of the fifty kept
 * scans are on disk right now.
 */
function showHistoryFooter() {
  const [latest] = history;

  lastScanValue.textContent = latest ? describeScan(latest) : 'No scans yet';
  keptScansValue.textContent = `${history.length} of ${HISTORY_KEEP} kept`;
}

/**
 * "29 Aug, 03:09 - 3 entries" - or just the count, for a date the field cannot read.
 *
 * A plain hyphen, not the middle dot this file already has one of (in the violations hint below):
 * a second literal non-ASCII byte pair here would be one more thing for a careless encoding
 * round-trip on this file to mangle, for a separator this string has no real need to match.
 */
function describeScan(entry) {
  const at = new Date(entry?.completedAt);
  const entries = Number.isFinite(entry?.entryCount) ? entry.entryCount : 0;
  const noun = entries === 1 ? 'entry' : 'entries';

  if (Number.isNaN(at.getTime())) {
    return `${entries} ${noun}`;
  }

  const when = at.toLocaleString('en-GB', {
    day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit',
  });

  return `${when} - ${entries} ${noun}`;
}

/**
 * Puts one profile's options into the fields - all of them, both masked credentials included.
 *
 * The password and the client secret ARE filled, unlike every earlier version of this window: the
 * host stores them encrypted under the Windows user and sends them back decrypted, so a saved
 * member scan with write-back is one click rather than two retyped credentials. See
 * DashboardSettings for what that protects and what it does not.
 *
 * A locale the profile names but this page does not offer is left alone rather than assigned,
 * because assigning it would clear the select instead of choosing something.
 */
function fillForm(profile) {
  urlInput.value = profile.url ?? '';
  maxPagesInput.value = profile.maxPages ?? NEW_SITE.maxPages;
  memberEmailInput.value = profile.memberEmail ?? '';
  memberPasswordInput.value = profile.memberPassword ?? '';
  clientIdInput.value = profile.clientId ?? '';
  // Empty for a profile saved without one, rather than falling back to anything: an empty box is
  // exactly what makes the host use the machine's variable, and prefilling the box with a secret
  // this site never registered would have the next Save write it into the profile for good.
  clientSecretInput.value = profile.clientSecret ?? '';
  // Blank rather than the placeholder's text: the field is empty for a site that never renamed its
  // consent cookie, and prefilling the default would make an untouched site look configured - and
  // would then be saved back as an explicit value the next time Save site was pressed.
  consentCookieInput.value = profile.consentCookie ?? '';

  // Restored from the profile like everything else in the grid, and unlike dry run: a recipient list
  // is a fact about the client that does not go stale between sessions, and the danger dry run
  // guards against - a stored value quietly arming something - runs the other way here. A profile
  // that says "do not email" restoring as unticked is the safe direction.
  emailEnabledInput.checked = profile.emailEnabled === true;
  emailToInput.value = profile.emailTo ?? '';
  emailCcInput.value = profile.emailCc ?? '';

  fillDryRun(profile.url ?? '');

  syncEmailFields();

  // Setting .value fires no input event, so the note under the pair and the sidebar's site label
  // are recomputed by hand.
  showSecretStatus();
  showBrandSite();

  // Every masked box goes back to masked. The profile on screen is not the one that was on screen a
  // moment ago, and a box left revealed would put this site's secret in front of whoever the last
  // site's was revealed for.
  maskAll();

  if (Array.from(localeInput.options).some((option) => option.value === profile.locale)) {
    localeInput.value = profile.locale;
  }
}

/**
 * Sets the dry-run box for one URL: whatever the operator last set it to for this URL in this
 * session, and checked for any URL they have not set it on yet.
 *
 * The profile's saved value is deliberately not consulted. It is the one setting where restoring
 * what was stored is the wrong move: a site saved after a real write-back would then hand back an
 * unticked box the moment it was selected, arming a write nobody had asked for in this session. So
 * every URL starts dry, every session, and only the operator's own tick changes that - which is why
 * the value is recorded here as well as in the change listener.
 */
function fillDryRun(url) {
  dryRunInput.checked = dryRunByUrl.has(url) ? dryRunByUrl.get(url) : true;

  dryRunByUrl.set(url, dryRunInput.checked);
}

/** The saved profile a dropdown value names, or null for "New site" and for anything unknown. */
function profileFor(url) {
  return url === '' ? null : sites.find((profile) => profile.url === url) ?? null;
}

/** Everything the run card currently holds, in the shape both `run` and `saveSite` carry. */
function currentProfile() {
  const requested = Number.parseInt(maxPagesInput.value, 10);

  return {
    url: urlInput.value,
    // A blank or unparseable field is sent as zero rather than as NaN, which JSON writes as null and
    // the host cannot read at all: the host answers a number it cannot use with the console tool's
    // own default, and stores that default rather than the zero.
    maxPages: Number.isFinite(requested) ? requested : 0,
    locale: localeInput.value,
    dryRun: dryRunInput.checked,
    memberEmail: memberEmailInput.value,
    memberPassword: memberPasswordInput.value,
    clientId: clientIdInput.value,
    clientSecret: clientSecretInput.value,
    consentCookie: consentCookieInput.value,
    emailEnabled: emailEnabledInput.checked,
    emailTo: emailToInput.value,
    emailCc: emailCcInput.value,
  };
}

/**
 * Redraws the dropdown from the host's list and refills the form from whichever profile it names.
 *
 * The one place the saved sites reach the page, so `state` at startup and every `sites` answer to a
 * save, a delete or a starting run all land in the same code - there is no second path that could
 * leave the list and the fields describing different profiles.
 *
 * The form is refilled unconditionally, including after a save of the values already in it. That
 * costs nothing visible and is what makes the host's normalisation show: a URL is stored trimmed
 * and a blank page count is stored as 25, and a form still showing the untyped version would then
 * Save something subtly different the next time it was pressed.
 */
function showSites(nextSites, selectedUrl) {
  sites = Array.isArray(nextSites) ? nextSites : [];

  // Everything after the first, which is "New site" and belongs to the markup rather than to the
  // list. Rebuilt rather than diffed: the list is a handful of entries and a rebuild cannot get out
  // of step with what the host just sent.
  while (siteSelect.options.length > 1) {
    siteSelect.remove(1);
  }

  for (const profile of sites) {
    siteSelect.add(new Option(profile.url, profile.url));
  }

  const wanted = typeof selectedUrl === 'string' ? selectedUrl : '';

  // Checked against the options rather than assigned blind: a select handed a value it has no
  // option for selects NOTHING - not the first option - and the dropdown would render blank rather
  // than falling back to "New site".
  siteSelect.value = sites.some((profile) => profile.url === wanted) ? wanted : '';

  fillForm(profileFor(siteSelect.value) ?? NEW_SITE);

  // The URL field decides which scans the chart is about, and it has just been rewritten.
  showTrend();
  syncSiteButtons();
}

/**
 * A state message carries only what changed, so each field is applied only when it is there: the
 * host posts `{ running }` around every scan and the fuller answer to `ready` once, at startup.
 */
function applyState(message) {
  if ('running' in message) {
    const wasRunning = running;

    setRunning(message.running);

    // Asked for when the scan ENDS, not when its result arrives: the host writes the history file
    // after it posts the result and before it posts this state, so a request made on the result
    // would race that write and draw a chart missing the run that had just finished.
    if (wasRunning && message.running === false) {
      post({ type: 'listHistory' });
    }
  }

  // Sent once, with ready. Left as it arrives rather than prefixed with a 'v' - it is the string
  // MinVer stamped, and the eyebrow above it already says what it is. Blank for a build with no
  // version attribute at all, which shows as an empty row rather than as "undefined".
  if (typeof message.version === 'string' && appVersionValue !== null) {
    appVersionValue.textContent = message.version;
  }

  if ('secretIsSet' in message) {
    // The variable's name comes from the host so it is spelled in one place - the same constant the
    // engine reads it with. A flag and a name, never the value: the note has five states and none of
    // them needs the secret itself. Left in the ordinary muted colour deliberately: report-only is a
    // supported mode, not a fault.
    secret = { isSet: message.secretIsSet === true, variable: message.secretVariable };

    // Assigned before showSites below refills the form, so the fill that follows already knows
    // whether there is a fallback to be quiet about; called here as well for the case where this
    // state arrives with the form already showing something.
    showSecretStatus();
  }

  // 'email' in message, not a truthiness test, for the same reason as 'sites' below: null is a real
  // answer - no account has ever been saved - and it has to empty the Email page rather than be
  // ignored. Applied BEFORE showSites, so the note the form fill draws under the recipients already
  // knows what this machine can send as.
  if ('email' in message) {
    showEmailAccount(message.email);
  }

  // 'sites' in message, not a truthiness test: an empty array is a real answer - a first launch, or
  // the last profile just deleted - and it has to clear the dropdown rather than be ignored.
  if ('sites' in message) {
    showSites(message.sites, message.selectedUrl);
  }

  // Load's decrypt failures, one line each. Printed rather than shown beside the field they belong
  // to: they are about a file read that has already finished, and the log is where this window says
  // what happened rather than what is wrong with the form.
  if (Array.isArray(message.warnings)) {
    for (const warning of message.warnings) {
      logPanel.append('warning', warning);
    }
  }
}

/**
 * Everything a finished scan puts on screen: the summary in the log, the four counts, the table.
 *
 * Nothing here recomputes what the host already decided. Each count is the length of a list on the
 * result and the summary is the host's own text, so the window cannot end up telling a different
 * story from the report on disk.
 */
function showResult(message) {
  // Appended as ONE entry rather than one line at a time, so the blank lines survive: the panel
  // renders a line as an <li>, and an <li> holding an empty string produces no line box and so no
  // height. Its white-space is pre-wrap, so one multi-line string keeps both the blank lines and the
  // leading spaces that put the json path under the markdown one.
  if (Array.isArray(message.summary) && message.summary.length > 0) {
    logPanel.append('info', message.summary.join('\n'));
  }

  const scan = message.scan;

  // The host always sends one. A result without it would take the tiles down with it, and the
  // summary above is worth keeping either way.
  if (scan === undefined || scan === null) {
    return;
  }

  const added = scan.outcome?.added?.length;

  tiles.entries.value = scan.candidates.length;
  // No outcome means the write-back was never attempted - not configured, or nothing to send - and
  // a hint reading "0 added last run" would be an answer to a question nobody asked.
  tiles.entries.hint = added === undefined ? '' : `${added} added last run`;

  tiles.violations.value = scan.violations.length;
  // The exit code is not recomputed here: 1 is what a violation means, and the tile says so in the
  // same breath as the number, because the number alone does not tell the operator the run failed.
  tiles.violations.hint = scan.violations.length > 0 ? 'fails the run · exit 1' : 'none';

  tiles.review.value = scan.candidates.filter((candidate) => candidate.flag === 'NeedsReview').length;
  tiles.expected.value = scan.expectedButNotObserved.length;

  findingsTable.result = scan;

  findings.hidden = false;
}

function requestRun() {
  if (runButton.hidden || runButton.disabled) {
    return;
  }

  // Cleared here, so one run's log is one run's log. The scrollback survives the end of a scan - it
  // is the next scan that replaces it, which is the only moment the old lines stop being the answer
  // to what is on screen.
  logPanel.clear();

  // Spread flat, because `run` carries its options at the top level while `saveSite` nests them
  // under `profile`. Both read the form through the same function on purpose: a run and the profile
  // that run writes must describe the same thing, and two readers of the same eight fields is
  // exactly how they would come to differ.
  post({ type: 'run', ...currentProfile() });
}

/**
 * Saves what the form holds as the profile for the URL it names.
 *
 * Editing the URL of a selected profile and pressing this is a "save as": the new URL matches no
 * saved profile, so a second one appears and the original stays until it is deleted. That is the
 * useful reading - copying a set of options from staging to production is the common case - and the
 * host's Upsert is where the same decision is written down.
 */
function requestSaveSite() {
  if (saveSiteButton.disabled) {
    return;
  }

  const profile = currentProfile();

  pendingSiteAction = { verb: 'saved', url: profile.url };

  post({ type: 'saveSite', profile });
}

/**
 * Forgets the selected profile.
 *
 * The dropdown's value, not the URL field's: the field can have been edited since the profile was
 * chosen, and deleting whatever happens to be typed is not what a Delete beside a dropdown means.
 */
function requestDeleteSite() {
  if (deleteSiteButton.disabled) {
    return;
  }

  pendingSiteAction = { verb: 'deleted', url: siteSelect.value };

  post({ type: 'deleteSite', url: siteSelect.value });
}

function requestCancel() {
  if (cancelButton.hidden || cancelButton.disabled) {
    return;
  }

  // Disabled on the way out, not on the way back: the engine only observes a cancel between passes,
  // and a second click in the meantime would ask for something already asked for.
  cancelButton.disabled = true;

  post({ type: 'cancel' });
}

/* ---------------------------------------------------------------- email

    Two halves that meet only here. The machine's mail account is one record on the Email page; who
    hears about each site is three fields in that site's profile. The window never guesses either -
    both travel to the host on the message that uses them.
*/

/**
 * The machine's mail account as the host last reported it, or null if none has ever been saved.
 * @type {{ host: string, port: number, security: string, username: string, password: string,
 *          fromAddress: string, fromName: string } | null}
 */
let emailAccount = null;

/** What the Email page shows before anything has been saved. */
const NEW_ACCOUNT = {
  host: '',
  port: 587,
  security: 'StartTls',
  username: '',
  password: '',
  fromAddress: '',
  fromName: '',
};

/**
 * The port that goes with each security mode.
 *
 * Used only to MOVE the port when the mode changes and the box still holds the old mode's default -
 * never to override a number the operator typed. Picking the mode is the decision; the port is
 * almost always the one that follows from it, and making someone remember 465 is a way to have them
 * get it wrong.
 */
const SECURITY_PORTS = { StartTls: 587, SslOnConnect: 465, None: 25 };

const flashTimers = new WeakMap();

/**
 * Shows one line in a status region for a few seconds, then fades it.
 *
 * The same behaviour as the site toast, per element rather than for the one region: three of these
 * exist now - saving a site, saving the account, sending from the History page - and one shared
 * timer would have the newest message cut short by the previous one's fade.
 */
function flash(element, text) {
  if (element === null) {
    return;
  }

  clearTimeout(flashTimers.get(element));

  element.textContent = text;
  element.hidden = false;
  element.classList.remove('is-fading');

  flashTimers.set(element, setTimeout(() => {
    element.classList.add('is-fading');

    flashTimers.set(element, setTimeout(() => {
      element.hidden = true;
      element.textContent = '';
    }, 450));
  }, 6000));
}

/** Whether an account has enough in it to attempt a send - the host's own rule, in one place. */
function accountIsUsable(account) {
  return account !== null
    && typeof account.host === 'string' && account.host.trim() !== ''
    && typeof account.fromAddress === 'string' && account.fromAddress.trim() !== '';
}

/** Fills the Email page from what the host reports, and recomputes everything that depends on it. */
function showEmailAccount(account) {
  emailAccount = account ?? null;

  const shown = emailAccount ?? NEW_ACCOUNT;

  emailHostInput.value = shown.host ?? '';
  emailPortInput.value = shown.port ?? NEW_ACCOUNT.port;
  emailUsernameInput.value = shown.username ?? '';
  emailPasswordInput.value = shown.password ?? '';
  emailFromAddressInput.value = shown.fromAddress ?? '';
  emailFromNameInput.value = shown.fromName ?? '';

  // Checked against the options rather than assigned blind, for the same reason the site dropdown
  // is: a select handed a value it has no option for selects nothing at all.
  if (Array.from(emailSecurityInput.options).some((option) => option.value === shown.security)) {
    emailSecurityInput.value = shown.security;
  }

  syncEmailControls();
  showEmailStatus();
}

/** Everything the Email page currently holds, in the shape saveEmail and testEmail carry. */
function currentAccount() {
  const port = Number.parseInt(emailPortInput.value, 10);

  return {
    host: emailHostInput.value,
    // The security mode's own default rather than zero for an unreadable box: a port is not
    // optional the way a page count is, and the host has no sensible number to substitute.
    port: Number.isFinite(port) && port > 0 ? port : SECURITY_PORTS[emailSecurityInput.value] ?? 587,
    security: emailSecurityInput.value,
    username: emailUsernameInput.value,
    password: emailPasswordInput.value,
    fromAddress: emailFromAddressInput.value,
    fromName: emailFromNameInput.value,
  };
}

/**
 * The line under the site's recipient boxes: what this machine would send AS.
 *
 * It is the half of the answer the Scan page cannot otherwise show. The boxes above it name who the
 * report goes to; whether anything can be sent at all is a property of the Email page, two clicks
 * away, and an operator filling in a recipient deserves to find out here rather than after a scan.
 */
function showEmailStatus() {
  if (emailRecipientsNote === null) {
    return;
  }

  emailRecipientsNote.textContent = accountIsUsable(emailAccount)
    ? `Sent from ${emailAccount.fromAddress.trim()} via ${emailAccount.host.trim()}. Change that on the Email page.`
    : 'No mail server is set up on this machine yet, so nothing can be sent. Fill in the Email page.';
}

/** Shows the recipient boxes only when this site is set to email anything. */
function syncEmailFields() {
  emailFields.hidden = emailEnabledInput.checked === false;
}

/**
 * Hides both Email report buttons while there is no account to send with.
 *
 * A disabled button would be the other option and is worse here: it offers something and refuses,
 * with no room beside it to say why. The note under the recipient boxes is where the "why" lives,
 * and it is on the page that also has the fields to fix it.
 */
function syncEmailControls() {
  const usable = accountIsUsable(emailAccount);

  if (scanEmailSendButton !== null) {
    scanEmailSendButton.hidden = usable === false;
  }

  if (historyEmailSendButton !== null) {
    historyEmailSendButton.hidden = usable === false;
  }

  if (usable === false) {
    closeBar(scanEmailBar);
    closeBar(historyEmailBar);
    closeBar(emailTestBar);
  }
}

function openBar(bar, input, prefill) {
  if (bar === null) {
    return;
  }

  bar.hidden = false;

  if (input !== null) {
    input.value = prefill ?? '';
    input.focus();
    input.select();
  }
}

function closeBar(bar) {
  if (bar !== null) {
    bar.hidden = true;
  }
}

/**
 * Mails one scan: the one the result card is showing, or the one a History row selected.
 *
 * `path` is what tells the two apart, and null is not an oversight - it means "the run this session
 * just finished", which the host holds itself. That is the case that has to work when the history
 * file could not be written, which is exactly when getting the findings out of this window matters
 * most.
 */
function requestSendEmail(input, path) {
  const to = input.value.trim();

  if (to === '') {
    return;
  }

  // The profile's Cc rides along with the Scan page's send and not with the History page's: on the
  // Scan page the operator can see the Cc box that is being honoured, and on the History page the
  // form may well be showing a different site than the row that is selected.
  post({
    type: 'sendEmail',
    to,
    cc: path === null ? emailCcInput.value : '',
    path,
  });
}

form.addEventListener('submit', (event) => {
  event.preventDefault();

  requestRun();
});

// Ticking the box reveals the boxes it is about. Nothing is sent and nothing is saved: like every
// other field in the grid, it reaches settings.json only through Save site or a run.
emailEnabledInput.addEventListener('change', syncEmailFields);

// Both send controls open a row rather than sending, and both prefill from the site's own recipient
// list - the common case is "the people this site always goes to", and the row exists so that list
// is visible and editable before anything leaves.
scanEmailSendButton?.addEventListener('click', () => {
  openBar(scanEmailBar, scanEmailManualTo, emailToInput.value);
});

historyEmailSendButton?.addEventListener('click', () => {
  openBar(historyEmailBar, historyEmailManualTo, emailToInput.value);
});

scanEmailBar?.addEventListener('submit', (event) => {
  event.preventDefault();

  requestSendEmail(scanEmailManualTo, null);
  closeBar(scanEmailBar);
});

historyEmailBar?.addEventListener('submit', (event) => {
  event.preventDefault();

  // Read at click time rather than captured when the row opened: the answer is matched against this
  // path by the host's error envelope, and the selection can have moved while the row was open.
  requestSendEmail(historyEmailManualTo, selectedHistoryPath);
  closeBar(historyEmailBar);
});

// One listener for every Cancel in every one of the three rows. They do exactly the same thing, and
// three identical handlers is three places for them to stop doing it.
document.addEventListener('click', (event) => {
  const cancel = event.target.closest?.('[data-mailbar-cancel]');

  if (cancel !== null && cancel !== undefined) {
    closeBar(cancel.closest('.mailbar'));
  }
});

emailForm?.addEventListener('submit', (event) => {
  event.preventDefault();

  post({ type: 'saveEmail', account: currentAccount() });
});

// Moves the port to the one that goes with the mode, but only when the box still holds the mode it
// is leaving. A number the operator typed is theirs.
emailSecurityInput?.addEventListener('change', () => {
  const current = Number.parseInt(emailPortInput.value, 10);

  if (Object.values(SECURITY_PORTS).includes(current)) {
    emailPortInput.value = SECURITY_PORTS[emailSecurityInput.value] ?? current;
  }
});

emailTestButton?.addEventListener('click', () => {
  openBar(emailTestBar, emailTestToInput, emailFromAddressInput.value);
});

// Sends what is ON SCREEN, saved or not: the question the button answers is "will this work", and
// testing the last saved values would answer it about the wrong ones the moment anything was edited.
emailTestSendButton?.addEventListener('click', () => {
  const to = emailTestToInput.value.trim();

  if (to === '') {
    return;
  }

  flash(emailToast, `Sending a test to ${to}...`);

  post({ type: 'testEmail', to, account: currentAccount() });

  closeBar(emailTestBar);
});

cancelButton.addEventListener('click', requestCancel);
saveSiteButton.addEventListener('click', requestSaveSite);
deleteSiteButton.addEventListener('click', requestDeleteSite);

// Picking a site replaces the whole form, including the URL - the dropdown is the profile, not a
// bookmark for one field. The host is not told: a selection is a local act until something is saved
// or run, so browsing the saved sites cannot rewrite settings.json.
siteSelect.addEventListener('change', () => {
  fillForm(profileFor(siteSelect.value) ?? NEW_SITE);

  // The URL field lives inside Options now. A saved site needs nothing typed, so the section can
  // stay closed; "New site" has nothing to run yet, and closing it back on the operator would leave
  // them with an empty dropdown and no visible place to put the address.
  if (siteSelect.value === '') {
    optionsDetails.open = true;
  }

  showTrend();
  syncSiteButtons();
});

// The chart is about the site in the box, so it follows the box rather than the last run: typing a
// different site there shows that site's trend without having to scan it first. Save site follows
// the same keystrokes, because an empty URL is nothing to save under.
urlInput.addEventListener('input', () => {
  showTrend();
  syncSiteButtons();
  showBrandSite();
});

// Keyed on the dropdown rather than on the URL box: the dropdown is which profile is being edited,
// and a half-typed address in the field above is not yet a site. '' - "New site" - is a key like any
// other, so a tick set while composing a new profile is still there after a glance elsewhere.
dryRunInput.addEventListener('change', () => {
  dryRunByUrl.set(siteSelect.value, dryRunInput.checked);
});

// Either box changes what the note under the pair should say, so both are watched: the note is
// about the two of them together, and one listener would leave it a keystroke stale half the time.
clientIdInput.addEventListener('input', showSecretStatus);
clientSecretInput.addEventListener('input', showSecretStatus);

// One button per masked box - the member password and the client secret here, the SMTP password on
// the Email page - in place of the one checkbox under the grid that used to unmask them together.
// The reason to reveal any of them is the same, which is why one piece of code serves all three:
// checking that what is in the box is what the operator meant to put there. Nothing is re-read or
// re-sent; only the input's own type changes, so a revealed box behaves exactly like a masked one.
//
// aria-pressed carries the state. The CSS draws the slash across the eye from that attribute, so
// what a screen reader is told and what is on screen cannot drift apart, and the label is rewritten
// with it - "show the password" is a lie the moment the password is showing.
function setRevealed(button, revealed) {
  const input = button.closest('.masked')?.querySelector('.input');

  if (!input) {
    return;
  }

  input.type = revealed ? 'text' : 'password';
  button.setAttribute('aria-pressed', String(revealed));

  const label = `${revealed ? 'Hide' : 'Show'} the ${button.dataset.reveals}`;

  button.setAttribute('aria-label', label);
  button.title = label;
}

/** Puts every masked box back to masked, wherever it is and whatever it was showing. */
function maskAll() {
  for (const button of revealButtons) {
    setRevealed(button, false);
  }
}

for (const button of revealButtons) {
  button.addEventListener('click', () => {
    setRevealed(button, button.getAttribute('aria-pressed') !== 'true');
  });
}

// Ctrl+Enter runs and Escape cancels, but only while the Scan page is the one on screen: a shortcut
// that fires from another page would act on a form the operator cannot see.
window.addEventListener('keydown', (event) => {
  if (scanPage?.hidden !== false) {
    return;
  }

  if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
    event.preventDefault();

    requestRun();

    return;
  }

  if (event.key === 'Escape') {
    requestCancel();
  }
});

// PostHistory re-reads the folder on every request rather than caching it, specifically so a scan
// run from a terminal while this window is open shows up here - and that only holds if the History
// page actually asks again on every visit, not just once at startup.
window.addEventListener('page-shown', (event) => {
  if (event.detail?.page === 'history') {
    post({ type: 'listHistory' });
  }
});

/**
 * The one path a `scan`/`error` answer is allowed to render, or null when that would not be exactly
 * one thing. `loadScan` carries no correlation id of its own, so this is what lets a reply that
 * arrives after the operator has already moved on get recognised as stale and dropped - see the
 * `scan`/`error` cases below.
 */
let selectedHistoryPath = null;

/**
 * Every path currently checked, as the list last reported it - what a `diff`/`error` answer to
 * `compare` is measured against.
 *
 * A separate variable from the one above rather than a derivation of it, because the two answer
 * different counts: exactly two is precisely the state in which `selectedHistoryPath` is null, so a
 * compare answer matched against that single path would be dropped every single time.
 */
let selectedHistoryPaths = [];

/**
 * Whether an answer's echoed pair is the pair still checked, regardless of the order it was asked
 * in.
 *
 * The host echoes `paths` in the order the page sent them - click order - so a comparison against a
 * selection the operator built the other way round has to ignore order. This is deliberately not the
 * ordering that decides "appeared": the host does that by completion time, and the two questions
 * must not be answered with the same list.
 */
function isSelectedPair(paths) {
  if (Array.isArray(paths) === false || paths.length !== 2 || selectedHistoryPaths.length !== 2) {
    return false;
  }

  const answered = [...paths].sort();
  const selected = [...selectedHistoryPaths].sort();

  return answered.every((path, index) => path === selected[index]);
}

/**
 * What the two counts this window answers mean: one selected is a scan to look at, two are a pair to
 * compare. Anything else asks the host for nothing.
 *
 * A pane is only ever HIDDEN here, never shown - the answer that arrives is what shows it. Revealing
 * one on the selection instead would put the previous scan's rows, or the previous pair's diff, on
 * screen for however long the host takes to read the files.
 */
historyList?.addEventListener('selection-changed', (event) => {
  const paths = Array.isArray(event.detail?.paths) ? event.detail.paths : [];

  selectedHistoryPaths = paths;
  selectedHistoryPath = paths.length === 1 ? paths[0] : null;

  historyList.note = describeSelection(paths);

  // Nothing ticked, nothing to untick. This is also what clears the button after the list prunes a
  // selection itself - a deleted row, or the fifty-scan prune - because that announces too.
  if (historyUnselectButton) {
    historyUnselectButton.hidden = paths.length === 0;
  }

  if (paths.length !== 1) {
    historyDetail.hidden = true;
  }

  // Both panes go away for every count that is not their own, which is what returns the pane to the
  // detail table when a diff is showing and the operator unchecks one of the two rows.
  if (paths.length !== 2) {
    historyDiff.hidden = true;
  }

  if (paths.length === 1) {
    post({ type: 'loadScan', path: paths[0] });

    return;
  }

  if (paths.length === 2) {
    post({ type: 'compare', pathA: paths[0], pathB: paths[1] });
  }
});

// Deleting a kept scan removes its file. Asked before it happens rather than offered back as an
// undo: there is nothing to undo with - the JSON is the scan, and the host does not keep a copy of
// what it deleted.
historyList?.addEventListener('remove-scan', (event) => {
  const { path, when } = event.detail ?? {};

  if (typeof path !== 'string' || path === '') {
    return;
  }

  if (window.confirm(`Delete the scan completed ${when}? This cannot be undone.`) === false) {
    return;
  }

  // The panes are about a selection that is about to stop existing. Hidden here rather than waiting
  // for the fresh list, so a pane cannot linger over a row that has gone.
  historyDetail.hidden = true;
  historyDiff.hidden = true;

  post({ type: 'deleteScan', path });
});

// No confirmation: it destroys nothing, and the ticks are two clicks to put back.
historyUnselectButton?.addEventListener('click', () => {
  historyList?.clearSelection();
});

historyClearButton?.addEventListener('click', () => {
  // Counted from what the page was last told, which is what the operator can see. The host clears
  // whatever is actually in the folder, so a scan that landed in between goes too - worth naming a
  // number anyway, because "all of them" is the thing being agreed to.
  const count = history.length;

  if (count === 0) {
    return;
  }

  if (window.confirm(`Delete all ${count} saved scan${count === 1 ? '' : 's'}? This cannot be undone.`) === false) {
    return;
  }

  historyDetail.hidden = true;
  historyDiff.hidden = true;

  post({ type: 'clearScans' });
});

/**
 * What the current selection is about to do, in one line for the list to show.
 *
 * The words live here and not in the component, exactly as cs-stat-tile's do: what a count of
 * checked boxes MEANS is this module's decision, and the list only ever reports what is checked.
 * Without this the comparison is undiscoverable - two checkboxes look like two checkboxes.
 */
function describeSelection(paths) {
  if (paths.length === 0) {
    return 'Tick one scan to see what it found, or two to compare them.';
  }

  if (paths.length === 1) {
    return 'One selected - its findings are below. Tick a second to compare the two.';
  }

  // The list itself holds the count at two - a third tick lets go of the first - so this is the last
  // state there is, and the place to say how to move on from it.
  return 'Two selected - the comparison is below. Tick another and the one ticked first gives way.';
}

host?.addEventListener('message', (event) => {
  // Already an object: the host posts with PostWebMessageAsJson, so WebView2 has parsed it by the
  // time it arrives.
  const message = event.data;

  switch (message?.type) {
    case 'log':
      logPanel.append(message.level, message.message);

      break;

    case 'state':
      applyState(message);

      break;

    case 'result':
      showResult(message);

      break;

    // The answer to saveSite and to deleteSite - and to a run STARTING, which saves the profile it
    // is about to run with before it starts. Always the host's whole list rather than the one entry
    // that changed, so the page never has to reconstruct what the file now holds from what it asked
    // for.
    case 'sites':
      showSites(message.sites, message.selectedUrl);

      // Only for a save or a delete the page asked for. A starting run posts this same answer after
      // its own upsert, and the log already says a scan began - a second line for it would be noise.
      if (pendingSiteAction !== null) {
        showToast(`Site ${pendingSiteAction.verb}: ${pendingSiteAction.url}`);
        pendingSiteAction = null;
      }

      break;

    // The answer to saveEmail: the account as the file now holds it. Refills the page for the same
    // reason `sites` refills the form - the host is what decides what was stored, and a page still
    // showing what it posted would save something subtly different next time.
    case 'email':
      showEmailAccount(message.account);

      flash(emailToast, 'Email settings saved.');

      break;

    // The answer to testEmail. Said where the button is rather than in the Scan page's log: the
    // operator is on the Email page looking at the fields this is a verdict on.
    case 'emailTested':
      flash(emailToast, message.message);

      break;

    // The answer to a manual sendEmail. A send from the result card has already reported itself in
    // the log panel beside it, so only a send from a History row needs saying again - which is what
    // the echoed path tells apart.
    case 'emailSent':
      if (typeof message.path === 'string') {
        flash(historyEmailToast, message.message);
      }

      break;

    case 'history':
      // Kept whole and filtered on the way to the chart, rather than filtered here: the History
      // page lists every site's scans, and re-asking the host for the same folder because the URL
      // field changed would be a file read per keystroke.
      history = Array.isArray(message.entries) ? message.entries : [];

      showTrend();
      showHistoryFooter();

      historyList.entries = history;
      historyList.note = describeSelection(selectedHistoryPaths);

      // Nothing kept, nothing to clear. Also the reset after Clear all: the button that emptied the
      // list is the first thing that should stop offering to.
      if (historyClearButton) {
        historyClearButton.hidden = history.length === 0;
      }

      break;

    // The answer to loadScan for a file that read back cleanly. Rendered into the SAME element the
    // Scan page uses - see cs-findings-table.js - so the colouring rule the exit code has to agree
    // with exists in exactly one place.
    //
    // Dropped, not rendered, when `message.path` is not what is still selected: loadScan carries no
    // correlation id, so an answer can arrive after the operator has already unchecked that row,
    // moved to a different one, or watched it fall out of a refreshed list. Rendering it anyway
    // would force the pane back open on data for a scan that is no longer the one thing selected -
    // exactly the invariant above, reached through the asynchronous door instead of the synchronous
    // one.
    case 'scan':
      if (message.path !== selectedHistoryPath) {
        break;
      }

      historyError.hidden = true;
      historyFindingsTable.result = message.result;
      historyDetail.hidden = false;

      break;

    // The answer to compare for two files that both read back cleanly. Ordered by completion time
    // by the host - see PostDiff - so "appeared" means the same thing whichever row was ticked
    // first, and nothing here re-derives it.
    //
    // Guarded the same way `scan` is, against the pair rather than the single path: `compare` has no
    // correlation id either, and two checked rows is exactly the state in which no single path is
    // selected. `paths` is echoed in the order it was asked and compared order-insensitively,
    // because the operator can tick the same two rows in either order and still be looking at the
    // same comparison.
    case 'diff':
      if (isSelectedPair(message.paths) === false) {
        break;
      }

      historyDiffError.hidden = true;
      historyDiffView.diff = message;
      historyDiff.hidden = false;

      break;

    // loadScan or compare answering a file that would not read back - deleted or corrupted since
    // the list was drawn. Shown inline rather than left silent: the operator asked for something
    // specific and is owed a reason it did not appear.
    //
    // Which request it answers is read off its SHAPE - one `path` for loadScan, two `paths` for
    // compare - rather than off a separate field naming the command. The echo is already the
    // correlation, and a second discriminator saying the same thing is a second thing that can
    // disagree with it. Each branch then applies its own staleness rule, for the same reason `scan`
    // and `diff` have theirs: an error for a selection that has already moved on must not force a
    // pane back open.
    case 'error':
      if (Array.isArray(message.paths)) {
        if (isSelectedPair(message.paths) === false) {
          break;
        }

        historyDiffView.diff = null;
        historyDiffError.textContent = message.message;
        historyDiffError.hidden = false;
        historyDiff.hidden = false;

        break;
      }

      if (message.path !== selectedHistoryPath) {
        break;
      }

      historyFindingsTable.result = null;
      historyError.textContent = message.message;
      historyError.hidden = false;
      historyDetail.hidden = false;

      break;

    // Nothing else: every envelope the host posts has an arm above. Ignored rather than logged, the
    // same as an unrecognised command on the host's side - a type this build has never heard of is a
    // mismatched pair of halves, not a fault worth a line in the operator's log.
    default:
      break;
  }
});

// Before ready rather than after it: the answer that fills the dropdown is milliseconds away, but
// until it arrives Save site would be a live button over an empty URL field. One call settles both
// buttons from the state the markup actually starts in.
syncSiteButtons();
showBrandSite();

// Last, and only once the page can answer: this is what releases the envelopes the host buffered
// while the window was still loading.
post({ type: 'ready' });

// After ready and never before it: the host only starts delivering once ready has arrived, and the
// two are answered in the order they were sent, so the profiles that name the site reach the page
// ahead of the history that has to be filtered by it.
post({ type: 'listHistory' });
