import { LitElement, css, html, nothing } from '@umbraco-cms/backoffice/external/lit';

/**
 * Generic Block Editor Custom View that previews a code snippet block in the
 * backoffice with real syntax highlighting, using a copy of highlight.js
 * bundled inside this package — so it works regardless of the consuming site.
 *
 * Register it from your own package/bundle against your content type, supplying
 * your property aliases via the manifest `meta`:
 *
 *   {
 *     type: 'blockEditorCustomView',
 *     alias: 'My.BlockView.Code',
 *     element: '/App_Plugins/Backoffice.BlockPreview/code-preview.element.js',
 *     elementName: 'backoffice-blockpreview-code',
 *     forContentTypeAlias: 'codeSnippetRow',
 *     forBlockEditor: 'block-list',
 *     meta: { codeAlias: 'code', captionAlias: 'title' }
 *   }
 *
 * Defaults: codeAlias = 'code', captionAlias = 'caption',
 * themeUrl = the bundled VS2015 dark theme.
 */

const DEFAULT_CODE_ALIAS = 'code';
const DEFAULT_CAPTION_ALIAS = 'caption';
const HLJS_SCRIPT_URL = '/App_Plugins/Backoffice.BlockPreview/lib/highlight.min.js';
const DEFAULT_THEME_URL = '/App_Plugins/Backoffice.BlockPreview/lib/vs2015.css';

let hljsPromise;
let copyPluginRegistered = false;

/** Copy-to-clipboard button plugin (equivalent to highlightjs-copy). */
const copyButtonPlugin = {
    'after:highlightElement'({ el, text }) {
        const parent = el.parentElement;
        if (!parent) return;
        const button = Object.assign(document.createElement('button'), {
            innerHTML: 'Copy',
            className: 'hljs-copy-button',
            type: 'button',
        });
        parent.classList.add('hljs-copy-wrapper');
        parent.appendChild(button);
        button.onclick = () => {
            if (!navigator.clipboard) return;
            navigator.clipboard.writeText(text).then(() => {
                button.innerHTML = 'Copied!';
                setTimeout(() => (button.innerHTML = 'Copy'), 2000);
            });
        };
    },
};

function ensureCopyPlugin(hljs) {
    if (hljs && !copyPluginRegistered) {
        hljs.addPlugin(copyButtonPlugin);
        copyPluginRegistered = true;
    }
    return hljs;
}

/** Loads the bundled highlight.js once and returns the global `hljs`. */
function loadHljs() {
    if (globalThis.hljs) return Promise.resolve(ensureCopyPlugin(globalThis.hljs));
    if (!hljsPromise) {
        hljsPromise = new Promise((resolve, reject) => {
            const done = () => resolve(ensureCopyPlugin(globalThis.hljs));
            const existing = document.querySelector(`script[src="${HLJS_SCRIPT_URL}"]`);
            if (existing) {
                existing.addEventListener('load', done, { once: true });
                if (globalThis.hljs) done();
                return;
            }
            const script = document.createElement('script');
            script.src = HLJS_SCRIPT_URL;
            script.onload = done;
            script.onerror = () => reject(new Error(`Failed to load highlight.js from ${HLJS_SCRIPT_URL}`));
            document.head.appendChild(script);
        });
    }
    return hljsPromise;
}

class BackofficeBlockPreviewCode extends LitElement {
    static properties = {
        content: { attribute: false },
        settings: { attribute: false },
        manifest: { attribute: false },
        _code: { state: true },
    };

    #highlightedCode;

    constructor() {
        super();
        this._code = '';
    }

    #alias(key, fallback) {
        const meta = this.manifest?.meta;
        return (meta && typeof meta[key] === 'string' && meta[key]) || fallback;
    }

    #themeUrl() {
        const meta = this.manifest?.meta;
        return (meta && typeof meta.themeUrl === 'string' && meta.themeUrl) || DEFAULT_THEME_URL;
    }

    willUpdate(changed) {
        if (changed.has('content') || changed.has('manifest')) {
            this._code = this.content?.[this.#alias('codeAlias', DEFAULT_CODE_ALIAS)] ?? '';
        }
    }

    updated() {
        this.#applyHighlighting();
    }

    async #applyHighlighting() {
        if (!this._code || this.#highlightedCode === this._code) return;
        const codeEl = this.renderRoot.querySelector('code.snippet');
        if (!codeEl) return;

        const hljs = await loadHljs();
        codeEl.textContent = this._code;
        codeEl.removeAttribute('data-highlighted');
        codeEl.className = 'snippet hljs';
        codeEl.parentElement
            ?.querySelectorAll('.hljs-copy-button')
            .forEach((node) => node.remove());

        hljs?.highlightElement(codeEl);
        this.#highlightedCode = this._code;
    }

    render() {
        const caption = this.content?.[this.#alias('captionAlias', DEFAULT_CAPTION_ALIAS)] ?? '';
        return html`
            <link rel="stylesheet" href=${this.#themeUrl()} />
            ${this._code
                ? html`<pre><code class="snippet hljs"></code></pre>`
                : html`<p class="empty">No code yet…</p>`}
            ${caption ? html`<p class="caption">${caption}</p>` : nothing}
        `;
    }

    static styles = css`
        :host {
            display: block;
            border: 1px solid var(--uui-color-border, #d8d7d9);
            border-radius: var(--uui-border-radius, 3px);
            padding: 1rem;
            background-color: #fff;
            box-sizing: border-box;
            color: #212529;
        }

        :host(:hover) {
            border-color: #2563eb;
            border-width: 2px;
            padding: calc(1rem - 1px);
        }

        pre {
            margin: 0;
            overflow: auto;
            font-size: 0.875em;
        }
        pre code.hljs {
            display: block;
            overflow-x: auto;
            padding: 1em;
            border-radius: var(--uui-border-radius, 3px);
            white-space: pre;
            font-family: SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono",
                "Courier New", monospace;
        }

        /* Copy button (added at runtime by the highlight.js plugin). */
        .hljs-copy-wrapper {
            position: relative;
        }
        .hljs-copy-button {
            position: absolute;
            /* Bottom-right keeps it clear of the block's top-right action
               toolbar (edit/settings/copy/delete) in the backoffice. */
            bottom: 0.5em;
            right: 0.5em;
            cursor: pointer;
            border: none;
            border-radius: 0.25em;
            padding: 0.35em 0.7em;
            font-size: 0.75rem;
            color: #fff;
            background: rgba(255, 255, 255, 0.15);
            opacity: 0;
            transition: opacity 0.2s ease;
        }
        .hljs-copy-wrapper:hover .hljs-copy-button,
        .hljs-copy-button:focus {
            opacity: 1;
        }

        .caption {
            font-size: 0.875rem;
            font-style: italic;
            text-align: center;
            margin: 0.625rem 0 0;
        }

        .empty {
            color: var(--uui-color-disabled-contrast, #868e96);
            font-style: italic;
            margin: 0;
        }
    `;
}

customElements.define('backoffice-blockpreview-code', BackofficeBlockPreviewCode);
export default BackofficeBlockPreviewCode;
