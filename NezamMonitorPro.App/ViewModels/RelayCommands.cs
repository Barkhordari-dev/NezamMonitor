using System.Windows.Input;

namespace NezamMonitorPro.App.ViewModels;

public sealed class AsyncRelayCommand : ICommand
{
    private readonly Func<Task> _execute;
    private readonly Func<bool>? _canExecute;

    // B-001: wired to CommandManager.RequerySuggested so WPF re-queries
    // CanExecute on focus/keyboard changes; RaiseCanExecuteChanged() below
    // forces a re-query on demand.
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public AsyncRelayCommand(Func<Task> execute, Func<bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public async void Execute(object? parameter) { try { await _execute(); } catch (Exception ex) { Services.ErrorLog.Write(ex, "AsyncRelayCommand"); } }
    public async Task ExecuteAsync() { try { await _execute(); } catch (Exception ex) { Services.ErrorLog.Write(ex, "AsyncRelayCommand"); } }
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    // B-001: see AsyncRelayCommand.
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public RelayCommand(Action execute, Func<bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)
    {
        // ERR-002: log, then rethrow so the global handler still sees it.
        try { _execute(); }
        catch (Exception ex)
        {
            Services.ErrorLog.Write(ex, "RelayCommand");
            throw;
        }
    }
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}

public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T?> _execute;
    private readonly Func<bool>? _canExecute;

    // B-001: see AsyncRelayCommand.
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public RelayCommand(Action<T?> execute, Func<bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)
    {
        // ERR-002: same treatment as RelayCommand (log, then rethrow).
        try
        {
            if (parameter is T t) _execute(t);
            else if (parameter == null) _execute(default);
            else if (parameter is T nullable) _execute(nullable);
        }
        catch (Exception ex)
        {
            Services.ErrorLog.Write(ex, "RelayCommand");
            throw;
        }
    }
    public void RaiseCanExecuteChanged() => CommandManager.InvalidateRequerySuggested();
}
