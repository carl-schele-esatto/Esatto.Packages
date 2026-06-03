import { LitElement, css, html, nothing } from '@umbraco-cms/backoffice/external/lit';

/**
 * Generic Block Editor Custom View that previews a YouTube video block in the
 * backoffice, styled to resemble a typical frontend player — without depending
 * on the consuming site's stylesheets.
 *
 * Register it from your own package/bundle against your content type, supplying
 * your property aliases via the manifest `meta`:
 *
 *   {
 *     type: 'blockEditorCustomView',
 *     alias: 'My.BlockView.Video',
 *     element: '/App_Plugins/Backoffice.BlockPreview/video-preview.element.js',
 *     elementName: 'backoffice-blockpreview-video',
 *     forContentTypeAlias: 'videoRow',
 *     forBlockEditor: 'block-list',
 *     meta: { urlAlias: 'videoUrl', captionAlias: 'caption' }
 *   }
 *
 * Defaults: urlAlias = 'videoUrl', captionAlias = 'caption'.
 */

const DEFAULT_URL_ALIAS = 'videoUrl';
const DEFAULT_CAPTION_ALIAS = 'caption';

/**
 * Extracts the YouTube video id from the common URL formats
 * (watch?v=, youtu.be/, embed/, shorts/) or a bare 11-char id.
 * @param {string | undefined} url
 * @returns {string | undefined}
 */
function getYouTubeId(url) {
    if (!url) return undefined;
    const patterns = [
        /[?&]v=([\w-]{11})/,
        /youtu\.be\/([\w-]{11})/,
        /\/embed\/([\w-]{11})/,
        /\/shorts\/([\w-]{11})/,
    ];
    for (const pattern of patterns) {
        const match = url.match(pattern);
        if (match) return match[1];
    }
    return /^[\w-]{11}$/.test(url.trim()) ? url.trim() : undefined;
}

class BackofficeBlockPreviewVideo extends LitElement {
    static properties = {
        // Set by the Block Editor Custom View framework.
        content: { attribute: false },
        settings: { attribute: false },
        manifest: { attribute: false },
    };

    #alias(key, fallback) {
        const meta = this.manifest?.meta;
        return (meta && typeof meta[key] === 'string' && meta[key]) || fallback;
    }

    render() {
        const url = this.content?.[this.#alias('urlAlias', DEFAULT_URL_ALIAS)];
        const caption = this.content?.[this.#alias('captionAlias', DEFAULT_CAPTION_ALIAS)] ?? '';
        const videoId = getYouTubeId(url);

        if (!videoId) {
            return html`<p class="empty">No video URL set…</p>`;
        }

        return html`
            <div class="youtube-player">
                <img
                    src="https://i.ytimg.com/vi/${videoId}/maxresdefault.jpg"
                    alt="YouTube video thumbnail" />
                <div class="play"></div>
            </div>
            ${caption ? html`<p class="caption">${caption}</p>` : nothing}
        `;
    }

    static styles = css`
        :host {
            display: block;
            /* Grey border by default; turns blue on hover (no layout shift). */
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
            /* shave 1px off padding so the 2px border doesn't shift the box */
            padding: calc(1rem - 1px);
        }

        /* Self-contained YouTube player styling (no dependency on site CSS). */
        .youtube-player {
            position: relative;
            width: 100%;
            background: #000;
            border-radius: 14px;
            aspect-ratio: 1280 / 720;
            overflow: hidden;
        }
        .youtube-player img {
            display: block;
            width: 100%;
            height: 100%;
            object-fit: cover;
        }
        .youtube-player .play {
            position: absolute;
            top: 50%;
            left: 50%;
            transform: translate3d(-50%, -50%, 0);
            width: 6.5em;
            height: 4em;
            background: #ff0000;
            opacity: 0.95;
            border-radius: 0.5em;
            box-shadow: 2px 3px 9px 2px #000;
            transition: opacity 0.2s cubic-bezier(0, 0, 0.2, 1);
        }
        .youtube-player:hover .play {
            opacity: 0.8;
        }
        .youtube-player .play::before {
            content: '';
            position: absolute;
            top: 50%;
            left: 50%;
            transform: translate3d(-50%, -50%, 0);
            border-color: transparent transparent transparent #fff;
            border-style: solid;
            border-width: 10px 0 10px 20px;
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

customElements.define('backoffice-blockpreview-video', BackofficeBlockPreviewVideo);
export default BackofficeBlockPreviewVideo;
