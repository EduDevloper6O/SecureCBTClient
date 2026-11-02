using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Elecctron.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private string _rollNumber = string.Empty;
    private string _examCode = string.Empty;
    private string _accessPassword = string.Empty;
    private string _statusMessage = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string, string, string>? OpenLobbyRequested;

    public string RollNumber
    {
        get => _rollNumber;
        set => SetField(ref _rollNumber, value);
    }

    public string ExamCode
    {
        get => _examCode;
        set => SetField(ref _examCode, value);
    }

    // Kept in memory for the live Apps Script login request; it is not saved to disk.
    public string AccessPassword
    {
        get => _accessPassword;
        set => SetField(ref _accessPassword, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetField(ref _statusMessage, value);
    }

    public ICommand ContinueCommand { get; }

    public MainWindowViewModel()
    {
        ContinueCommand = new RelayCommand(ContinueToLobby);
    }

    private void ContinueToLobby()
    {
        if (string.IsNullOrWhiteSpace(RollNumber))
        {
            StatusMessage = "Enter your application or roll number to continue.";
            return;
        }

        if (string.IsNullOrWhiteSpace(AccessPassword))
        {
            StatusMessage = "Enter your password or access PIN to continue.";
            return;
        }

        if (string.IsNullOrWhiteSpace(ExamCode))
        {
            StatusMessage = "Enter the exam code to continue.";
            return;
        }

        StatusMessage = "Connecting to the exam service...";
        OpenLobbyRequested?.Invoke(RollNumber.Trim(), AccessPassword, ExamCode.Trim().ToUpperInvariant());
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;

    public RelayCommand(Action execute) => _execute = execute;

    public event EventHandler? CanExecuteChanged
    {
        add => System.Windows.Input.CommandManager.RequerySuggested += value;
        remove => System.Windows.Input.CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => true;
    public void Execute(object? parameter) => _execute();
}
