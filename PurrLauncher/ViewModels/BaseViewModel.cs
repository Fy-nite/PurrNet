using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PurrLauncher.ViewModels;

public abstract class BaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private bool _isBusy;
    private string _title = string.Empty;

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public bool IsNotBusy => !_isBusy;

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    protected void SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        OnPropertyChanged(name);

        // Keep IsNotBusy in sync automatically
        if (name == nameof(IsBusy))
            OnPropertyChanged(nameof(IsNotBusy));
    }

    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
