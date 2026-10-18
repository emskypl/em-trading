using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EmTrading.Presentation.Localization;
using EmTrading.Presentation.Shell;
using Microsoft.Extensions.Logging;

namespace EmTrading.Presentation.Settings;

/// <summary>
/// Language (applied immediately) and defaults for new sessions, plus read-only facts about the installation.
/// </summary>
public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly IUserSettingsStore _store;
    private UserSettings _settings;

    public SettingsViewModel(IUserSettingsStore store, UserSettings settings, AppEnvironmentInfo environment, StatusViewModel status, ILogger<SettingsViewModel> logger)
        : base(status, logger)
    {
        _store = store;
        _settings = settings;
        Environment = environment;
        _selectedCulture = Loc.SupportedCultures.FirstOrDefault(c => c.Name == settings.Culture) ?? Loc.Polish;
        _defaultSymbols = settings.DefaultSymbols;
        _defaultInitialCapital = settings.DefaultInitialCapital;
    }

    public AppEnvironmentInfo Environment { get; }

    public IReadOnlyList<CultureInfo> Cultures => Loc.SupportedCultures;

    [ObservableProperty] private CultureInfo _selectedCulture;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = " ")]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _defaultSymbols;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Range(typeof(decimal), "1", "1000000000000", ErrorMessage = " ")]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private decimal _defaultInitialCapital;

    partial void OnSelectedCultureChanged(CultureInfo value)
    {
        Loc.Instance.SetCulture(value);
        Persist(_settings with { Culture = value.Name });
    }

    private bool CanSave() => !HasErrors;

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        Persist(_settings with { DefaultSymbols = DefaultSymbols.Trim(), DefaultInitialCapital = DefaultInitialCapital });
        Status.Show(Loc.Get("Status_SettingsSaved"));
    }

    private void Persist(UserSettings settings)
    {
        _settings = settings;
        _store.Save(settings);
    }
}
