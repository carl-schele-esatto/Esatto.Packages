# Backoffice.WorkspaceActionSpinner

Restores the post-modal spinner on Umbraco 17 workspace action buttons when those actions gate the actual operation behind a culture-picker modal.

Covered actions:

- **Save** / **Save and publish** (the `…` variants visible on multi-culture sites)
- **Publish with descendants** (chevron dropdown)
- **Schedule** (chevron dropdown)

Without this shim, clicking one of these actions opens a culture-picker modal, the user picks cultures and clicks Publish, the modal closes — and then 5+ seconds of validate + save + publish happens server-side with the only feedback being the thin progress bar on the tree node, far from where the user just clicked. This shim adds the spinner back on the button itself for that post-modal phase.

Pure App_Plugins package — no C# code, no `Startup` configuration. Drops in via NuGet and lights up the moment the assembly is referenced.

## Install

```bash
dotnet add package Backoffice.WorkspaceActionSpinner
```

Restart the site / hard-refresh the backoffice.

## What it does (one paragraph)

A document-level capture-phase click listener finds the `<umb-workspace-action>` element via `composedPath`. If the action has `_additionalOptions === true` (the modal-flow variant), the shim waits for one of the relevant publish/save modals (`umb-document-publish-modal`, `umb-document-save-modal`, `umb-document-publish-with-descendants-modal`, `umb-document-schedule-modal`) to open inside `<umb-backoffice-modal-container>`'s shadow root, then waits for it to close. After a 200 ms grace (so cancel paths — which resolve `execute()` near-instantly — settle without producing a spinner), it sets `_buttonState = 'waiting'` and `opacity = 0.6` on the action element. On the `action-executed` event, opacity is restored; for menu-item flows where Umbraco core never sets `_buttonState = 'success'` itself, the shim promotes `'waiting' → 'success'` and queues a 2s reset. Re-clicks while waiting are blocked at the capture phase (UUI's `state="waiting"` alone does not disable the button).

## Compatibility

| Umbraco | Status |
|---------|--------|
| 17.x    | Verified |

The shim depends on internal Umbraco implementation details:

- `<umb-workspace-action>` element with TypeScript `private _buttonState` / `_additionalOptions` (compiled to public JS properties)
- `<umb-backoffice-modal-container>` element with shadow-DOM modal rendering
- `action-executed` event from `UmbActionExecutedEvent`
- The four modal element tags listed above

If Umbraco patches the post-modal feedback gap upstream — or switches to true ES `#privateField` for `_buttonState` — remove the package and let upstream behaviour take over.

## License

MIT.
