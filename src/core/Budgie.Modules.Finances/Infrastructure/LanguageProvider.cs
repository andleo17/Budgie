using Budgie.Shared.Abstractions;

namespace Budgie.Modules.Finances.Infrastructure;

internal sealed class LanguageProvider(IUserSettingsProvider userSettingsProvider) : ILanguageProvider
{
    private string? _languageCode;

    public string LanguageCode
    {
        get
        {
            if (_languageCode == null)
            {
                _languageCode = userSettingsProvider.GetLanguageAsync().GetAwaiter().GetResult();
            }
            return _languageCode;
        }
    }
}
