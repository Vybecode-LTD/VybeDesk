using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ClaudePM.App.ViewModels;

/// <summary>
/// One question in a stepped wizard. Carries the question title, its list of
/// option choices, and the currently-selected token. Reusable across any
/// future wizard (Session Builder, future Skill Library, etc.) — not
/// Testing-Manager-specific.
///
/// The View binds an inner <c>ItemsControl</c> to <see cref="Options"/> and
/// each RadioButton's <c>IsChecked</c> to its <see cref="QuestionOption.IsSelected"/>
/// flag (one-way). Selection is driven through the <see cref="PickCommand"/>
/// which sets <see cref="SelectedToken"/> and syncs the option flags.
/// </summary>
public sealed partial class QuestionViewModel : ObservableObject
{
    public string Title { get; }
    public IReadOnlyList<QuestionOption> Options { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnswered))]
    private string _selectedToken;

    /// <summary>Has the user picked any option for this question yet?</summary>
    public bool IsAnswered => !string.IsNullOrEmpty(SelectedToken);

    public QuestionViewModel(
        string title,
        IEnumerable<QuestionOption> options,
        string initialToken = "")
    {
        Title = title;
        Options = new ReadOnlyCollection<QuestionOption>(options.ToList());
        _selectedToken = initialToken;
        SyncOptionFlags();
    }

    [RelayCommand]
    private void Pick(string? token)
    {
        if (token is null) return;
        SelectedToken = token;
        SyncOptionFlags();
    }

    /// <summary>
    /// Push <see cref="SelectedToken"/> down into each option's
    /// <see cref="QuestionOption.IsSelected"/> flag so View RadioButtons
    /// can bind one-way to a plain bool. Avalonia can't take a Binding as
    /// ConverterParameter, so this fanout is what keeps the binding clean.
    /// </summary>
    private void SyncOptionFlags()
    {
        foreach (var o in Options)
            o.IsSelected = o.Token == SelectedToken;
    }
}

/// <summary>
/// One radio-button choice inside a <see cref="QuestionViewModel"/>. The
/// <see cref="Token"/> is the stored value (matches the catalog tokens in
/// <see cref="ClaudePM.Core.Models.QuestionnaireAnswers"/>); the
/// <see cref="Label"/> is what the user sees.
/// </summary>
public sealed partial class QuestionOption : ObservableObject
{
    public string Token { get; }
    public string Label { get; }

    [ObservableProperty]
    private bool _isSelected;

    public QuestionOption(string token, string label)
    {
        Token = token;
        Label = label;
    }
}
