using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Esatto.Umbraco.Backoffice.CustomEditors.EncryptedTextbox;

/// <summary>Registers the value encryptor. The DataEditor and value converter are auto-discovered.</summary>
public sealed class EncryptedTextboxComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.Services.AddSingleton<IValueEncryptor, DataProtectionValueEncryptor>();
}
