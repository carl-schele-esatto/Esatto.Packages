using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Blocks;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;

namespace Esatto.Umbraco.Backoffice.CookieBanner;

/// <summary>
/// Creates and publishes a cookie policy page so a fresh install has somewhere for the banner and
/// the footer link to point at, pre-declaring the three cookies that are generic to every Umbraco
/// site. Idempotent: once a policy page exists - the package's or the site's own - this does
/// nothing on every later boot.
/// </summary>
internal sealed class CookieBannerContentSeeder(
    IContentService contentService,
    IContentTypeService contentTypeService,
    IEntityService entityService,
    IJsonSerializer jsonSerializer,
    IOptions<CookieBannerOptions> options,
    ILogger<CookieBannerContentSeeder> logger)
{
    // IContentService still only takes an integer user id, so the obsolete constant is the only
    // option here. Swap to SuperUserKey once the content service exposes key-based overloads.
#pragma warning disable CS0618
    private const int UserId = Constants.Security.SuperUserId;
#pragma warning restore CS0618

    private static readonly string[] AllCultures = ["*"];

    public void EnsurePolicyPage()
    {
        // DO NOT replace this with contentService.GetById(Guid). This package compiles once
        // against the Umbraco.Cms.Core 17.0.0 floor and ships that single DLL to run against
        // either a 17.x or an 18.x host - it is never recompiled per major. In 17.0.0,
        // IContentService directly declares GetById(Guid) (hiding the identical member it also
        // inherits from IContentServiceBase<IContent>); in 18.1.1 that direct declaration was
        // dropped from IContentService, leaving the member reachable only through inheritance.
        // A fresh compile against either version alone succeeds either way, so this is invisible
        // by inspection - but it is not invisible to a compiled binary: confirmed with a real
        // cross-version repro (a library built against 17.0.0 calling IContentService.GetById
        // (Guid), then loaded into a host referencing 18.1.1) throws
        // System.MissingMethodException at that exact call site, undocumented on either the 18
        // breaking-changes page or its release notes.
        // IEntityService.Exists(Guid, UmbracoObjectTypes) has no such hazard - declared directly
        // and identically on both versions, confirmed safe by the same cross-version repro - and
        // an existence check is all this method needs.
        if (entityService.Exists(CookieBannerKeys.Nodes.CookiePolicy, UmbracoObjectTypes.Document))
        {
            return;
        }

        IContentType? contentType = contentTypeService.Get(CookiePolicyPageResolver.ContentTypeAlias);
        if (contentType is null)
        {
            logger.LogWarning(
                "Skipping the cookie policy page: the '{Alias}' document type does not exist yet.",
                CookiePolicyPageResolver.ContentTypeAlias);
            return;
        }

        // An editor - or a consuming site's own seeder, which is the NDSTK migration path - may
        // already have a policy page under a different key. Never add a second one.
        // IContentService.GetPagedOfType declares `filter` as non-nullable on the interface even
        // though null for "no filter" is the documented, supported usage.
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type.
        if (contentService.GetPagedOfType(contentType.Id, 0, 1, out _, null, null).Any())
        {
            return;
        }
#pragma warning restore CS8625

        IContent? root = contentService.GetRootContent().FirstOrDefault();
        if (root is null)
        {
            // No site root yet, so there is nothing to parent a page to and a root-level policy
            // page would read as a second site. A later boot, once the site has a home page,
            // picks this up.
            logger.LogInformation(
                "Skipping the cookie policy page: the content tree has no root node yet.");
            return;
        }

        IContent policy = contentService.Create(
            "Cookies", root.Id, CookiePolicyPageResolver.ContentTypeAlias, UserId);
        policy.Key = CookieBannerKeys.Nodes.CookiePolicy;
        policy.SetValue("heading", "Cookies on this site");
        policy.SetValue(
            "introduction",
            "<p>We use cookies to make this site work. Below you can see exactly which cookies "
            + "we set, why, and how long they are kept.</p>");
        policy.SetValue(
            "outro",
            "<p>You can also block and delete cookies in your browser settings. Editors: replace "
            + "this text, and add any cookies set by services this site embeds.</p>");

        // Only the cookies every Umbraco site sets regardless of what it embeds. An invented
        // table would be worse than a short one, so the rest is left to an editor and to the
        // scanner package.
        policy.SetValue("cookies", BlockList(
            Block(CookieBannerKeys.ElementTypes.CookieDefinition,
                // Read from options, never hardcoded: a consumer that pins CookieName so its
                // existing visitors are not re-prompted must not end up with a policy page
                // declaring a cookie name the site does not set.
                ("cookieName", options.Value.CookieName),
                ("provider", "This website"),
                ("category", Dropdown("necessary")),
                ("purpose", "Stores your cookie choices so we do not have to ask again."),
                ("duration", $"{options.Value.CookieLifetimeDays} days"),
                ("storageType", Dropdown("Cookie"))),
            Block(CookieBannerKeys.ElementTypes.CookieDefinition,
                ("cookieName", ".AspNetCore.Antiforgery.*"),
                ("provider", "This website"),
                ("category", Dropdown("necessary")),
                ("purpose", "Protects forms against cross-site request forgery."),
                ("duration", "Session"),
                ("storageType", Dropdown("Cookie"))),
            Block(CookieBannerKeys.ElementTypes.CookieDefinition,
                ("cookieName", "UMB_MEMBER"),
                ("provider", "Umbraco"),
                ("category", Dropdown("necessary")),
                ("purpose", "Keeps a signed-in member logged in."),
                ("duration", "Session"),
                ("storageType", Dropdown("Cookie")))));

        contentService.Save(policy, UserId);

        PublishResult result = contentService.Publish(policy, AllCultures, UserId);
        if (result.Success is false)
        {
            // The node now exists under CookieBannerKeys.Nodes.CookiePolicy, so the entityService
            // .Exists(...) guard at the top of this method makes every LATER boot return
            // immediately - this publish is never retried. Left at Warning, the page stays an
            // invisible, unpublished draft indefinitely with nothing but a log line nobody
            // necessarily reads. Error, plus telling the operator exactly what to do about it.
            logger.LogError(
                "Created the cookie policy page but could not publish it: {Status}. It will NOT be "
                    + "retried automatically on a later boot - it now exists as an unpublished "
                    + "draft under key {Key}. Publish it manually in the backoffice, or delete it "
                    + "so the next boot recreates and republishes it.",
                result.Result,
                CookieBannerKeys.Nodes.CookiePolicy);
            return;
        }

        logger.LogInformation(
            "Created and published the cookie policy page under '{Root}'.", root.Name);
    }

    /// <summary>The flexible dropdown always stores an array, even in single-value mode.</summary>
    private string Dropdown(string value) => jsonSerializer.Serialize(new[] { value });

    private static BlockItemData Block(Guid elementTypeKey, params (string Alias, object Value)[] values)
        => new()
        {
            Key = Guid.NewGuid(),
            ContentTypeKey = elementTypeKey,
            Values = values
                .Select(value => new BlockPropertyValue { Alias = value.Alias, Value = value.Value })
                .ToList(),
        };

    /// <summary>
    /// Assembles the Block List property value: the layout referencing each block, the block
    /// content itself, and the "expose" list that marks the blocks as visible.
    /// </summary>
    private string BlockList(params BlockItemData[] blocks)
    {
        var value = new BlockListValue
        {
            Layout = new Dictionary<string, IEnumerable<IBlockLayoutItem>>
            {
                [Constants.PropertyEditors.Aliases.BlockList] =
                    blocks.Select(block => new BlockListLayoutItem(block.Key)).ToArray(),
            },
            ContentData = [.. blocks],
            SettingsData = [],
            Expose = blocks.Select(block => new BlockItemVariation(block.Key, null, null)).ToList(),
        };

        return jsonSerializer.Serialize(value);
    }
}
