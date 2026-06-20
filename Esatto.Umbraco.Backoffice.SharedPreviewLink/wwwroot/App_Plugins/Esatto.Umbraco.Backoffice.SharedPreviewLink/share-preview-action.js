import { UmbWorkspaceActionBase } from '@umbraco-cms/backoffice/workspace';
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/document';
import { umbHttpClient } from '@umbraco-cms/backoffice/http-client';
import { tryExecute } from '@umbraco-cms/backoffice/resources';
import { UMB_APP_LANGUAGE_CONTEXT } from '@umbraco-cms/backoffice/language';

// Share Preview workspace action — click logic.
//
// Pairs with share-preview-button.js (the custom element). The element is
// the button + modal UI; this class handles the click.
//
// Flow:
//   1. Save the current draft via ctx.requestSave() (same method the built-in
//      Save action calls — verified against Umbraco 17 source)
//   2. POST /umbraco/management/api/v1/backoffice/preview-link with contentKey
//   3. Stash { url, expiresAt } so the element can render a modal
//
// Auth: uses umbHttpClient which auto-attaches the bearer token (Management
// API auth scheme). Server endpoint is gated by
// [Authorize(Policy = AuthorizationPolicies.SectionAccessContent)].

const API_BASE = '/umbraco/management/api/v1/backoffice/preview-link';
const SECURITY = [{ scheme: 'bearer', type: 'http' }];

export class BackofficePreviewLinkWorkspaceAction extends UmbWorkspaceActionBase {
    constructor(host, manifest) {
        super(host, manifest);
        this.shareData = null;
        this._onShareReady = null;
    }

    onShareReady(callback) {
        this._onShareReady = callback;
    }

    async execute() {
        const ctx = await this.getContext(UMB_DOCUMENT_WORKSPACE_CONTEXT);
        if (!ctx) {
            throw new Error('The document workspace context is missing');
        }

        await ctx.requestSave();

        const contentKey = ctx.getUnique();
        if (!contentKey) {
            throw new Error('Document unique key unavailable after save');
        }

        // Culture of the variant the editor is actively VIEWING in the document workspace
        // (the open language tab / split-view pane) — the server builds the preview URL for
        // THIS culture. This follows the variant you're viewing, NOT the checkbox in the
        // Save dialog. Falls back to the global app language, then (server-side) to the
        // content's domain culture for invariant content.
        let culture = null;
        try {
            const activeVariants = ctx.splitView?.getActiveVariants?.() ?? [];
            culture = activeVariants.find((v) => v?.culture)?.culture ?? null;
            if (!culture) {
                const appLanguage = await this.getContext(UMB_APP_LANGUAGE_CONTEXT);
                culture = appLanguage?.getAppCulture?.() ?? null;
            }
        } catch {
            // context unavailable → let the server fall back to the domain culture
        }

        const { data, error } = await tryExecute(
            this,
            umbHttpClient.post({
                url: API_BASE,
                body: { contentKey, culture },
                security: SECURITY,
            }),
        );

        if (error) {
            throw new Error(`Preview link mint failed: ${error.message ?? error}`);
        }
        if (!data) {
            throw new Error('Preview link mint returned empty response');
        }

        this.shareData = data;
        if (this._onShareReady) {
            this._onShareReady(this.shareData);
        }
    }
}

export { BackofficePreviewLinkWorkspaceAction as api };
export default BackofficePreviewLinkWorkspaceAction;
