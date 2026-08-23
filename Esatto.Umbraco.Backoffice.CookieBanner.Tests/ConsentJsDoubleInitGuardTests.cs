using System;
using System.IO;
using Jint;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

/// <summary>
/// Pins the fix for the "the policy page's buttons are inert unless another tag helper is present"
/// defect: <c>consent.js</c> can now be referenced by two <c>&lt;script&gt;</c> tags on the same page
/// - <c>&lt;consent-banner /&gt;</c>'s and the one <c>Views/CookiePolicy.cshtml</c> now renders for
/// itself - and each tag is its own script inclusion, so without a guard the whole file (including
/// the document-level click listener near the bottom) would run, and register, twice.
/// </summary>
/// <remarks>
/// This actually EXECUTES the real <c>wwwroot/esatto-cookiebanner/consent.js</c> file (via Jint)
/// twice in one shared global scope - modelling exactly what two same-page <c>&lt;script src&gt;</c>
/// tags do in a browser - rather than merely asserting the guard's source text is present. A
/// text-only check could not tell a real early-return from a comment that merely claims one.
/// </remarks>
public sealed class ConsentJsDoubleInitGuardTests
{
    // bin/<Config>/net10.0 -> test project -> repo root, matching PackagingMetadataTests's RepoRoot.
    private static string RepoRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    private static string ConsentJsPath => Path.Combine(
        RepoRoot, "Esatto.Umbraco.Backoffice.CookieBanner", "wwwroot", "esatto-cookiebanner", "consent.js");

    /// <summary>
    /// A deliberately minimal DOM shim: just enough for the script to reach
    /// <c>window.cookieConsent = {...}</c> without throwing, with every attribute lookup answering
    /// "not set" (so every option takes its default) and no dialog element present (so the
    /// dialog-specific branches - all guarded by <c>if (dialog) {...}</c> or <c>if (!dialog) return;</c>
    /// in the real script - are simply skipped). <c>document.addEventListener</c> counts how many
    /// times a 'click' handler is registered: the real script registers exactly one, unconditionally,
    /// near the end of the IIFE, which is what the guard must stop from happening twice.
    /// </summary>
    private const string DomShim = """
        var window = {};
        var __clickListenerCount = 0;
        var document = {
            currentScript: { getAttribute: function (name) { return null; } },
            cookie: '',
            getElementById: function (id) { return null; },
            addEventListener: function (type, handler) {
                if (type === 'click') { __clickListenerCount = __clickListenerCount + 1; }
            }
        };
        """;

    [Fact]
    public void Executing_consent_js_twice_registers_the_click_handler_only_once()
    {
        var script = File.ReadAllText(ConsentJsPath);
        var engine = new Engine();

        engine.Execute(DomShim);

        // First execution: a normal, single <script> load. Behaviour must be unchanged from before
        // the guard existed.
        engine.Execute(script);
        Assert.Equal(1, (int)engine.Evaluate("__clickListenerCount").AsNumber());
        Assert.Equal("object", engine.Evaluate("typeof window.cookieConsent").AsString());

        // Second execution in the SAME global scope: what a second <script src="consent.js"> tag on
        // the same page does. Without the guard this re-runs the whole IIFE, including the
        // unconditional document.addEventListener('click', ...) call, bumping the count to 2.
        engine.Execute(script);
        Assert.Equal(1, (int)engine.Evaluate("__clickListenerCount").AsNumber());
    }
}
