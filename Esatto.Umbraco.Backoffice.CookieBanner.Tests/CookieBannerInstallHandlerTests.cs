using System;
using System.Linq;
using System.Reflection;
using Esatto.Umbraco.Backoffice.CookieBanner;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Xunit;

namespace Esatto.Umbraco.Backoffice.CookieBanner.Tests;

public class CookieBannerInstallHandlerTests
{
    [Fact]
    public void Composer_is_public_so_the_Umbraco_type_loader_discovers_it()
    {
        // Pins the package's entire zero-config promise. Umbraco's TypeLoader only scans PUBLIC
        // IComposer implementations in referenced assemblies; marking the composer internal - an
        // easy tidy-up, since everything it registers is internal - would silently install
        // nothing at all, with no error anywhere.
        Type composer = typeof(CookieBannerComposer);

        Assert.True(composer.IsPublic, "CookieBannerComposer must be public or Umbraco will not find it.");
        Assert.True(composer.IsSealed);
        Assert.Contains(typeof(IComposer), composer.GetInterfaces());
        Assert.NotNull(composer.GetConstructor(Type.EmptyTypes));
    }

    [Fact]
    public void Install_handler_runs_on_application_STARTED_not_starting()
    {
        // Pins the notification choice. UmbracoApplicationStartingNotification fires before the
        // content, content type, dictionary and language services can be used, so wiring the
        // installer there (as the sibling Redirects migration legitimately does, because a SQL
        // migration CAN run that early) would fail on a cold boot.
        Type handler = typeof(CookieBannerInstallHandler);

        Assert.Contains(
            typeof(INotificationAsyncHandler<UmbracoApplicationStartedNotification>),
            handler.GetInterfaces());
        Assert.DoesNotContain(
            typeof(INotificationAsyncHandler<UmbracoApplicationStartingNotification>),
            handler.GetInterfaces());
    }

    [Fact]
    public void Install_handler_takes_the_runtime_state_so_it_can_gate_on_RuntimeLevel_Run()
    {
        // Pins the gate. On an Install/Upgrade/BootFailed runtime the services this handler calls
        // are half-initialised; running the schema installer there is how a site ends up with a
        // partially created content model that the next boot then treats as already installed.
        ConstructorInfo constructor = typeof(CookieBannerInstallHandler)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .Single();

        Assert.Contains(
            typeof(IRuntimeState),
            constructor.GetParameters().Select(parameter => parameter.ParameterType));
    }
}
