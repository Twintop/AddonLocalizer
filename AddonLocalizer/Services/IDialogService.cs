namespace AddonLocalizer.Services;

public interface IDialogService
{
    Task<bool> ShowConfirmationAsync(string title, string message, string acceptButton = "Yes", string cancelButton = "No");
    Task ShowAlertAsync(string title, string message, string button = "OK");
    Task<string?> ShowPromptAsync(string title, string message, string acceptButton = "OK", string cancelButton = "Cancel", string? placeholder = null, string? initialValue = null);
}

public class DialogService : IDialogService
{
    public Task<bool> ShowConfirmationAsync(string title, string message, string acceptButton = "Yes", string cancelButton = "No")
    {
        return Application.Current?.MainPage?.DisplayAlert(title, message, acceptButton, cancelButton) ?? Task.FromResult(false);
    }

    public Task ShowAlertAsync(string title, string message, string button = "OK")
    {
        return Application.Current?.MainPage?.DisplayAlert(title, message, button) ?? Task.CompletedTask;
    }

    public Task<string?> ShowPromptAsync(string title, string message, string acceptButton = "OK", string cancelButton = "Cancel", string? placeholder = null, string? initialValue = null)
    {
        return Application.Current?.MainPage?.DisplayPromptAsync(title, message, acceptButton, cancelButton, placeholder, -1, null, initialValue ?? string.Empty) ?? Task.FromResult<string?>(null);
    }
}
