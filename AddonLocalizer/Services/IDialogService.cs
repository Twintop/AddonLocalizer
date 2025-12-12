namespace AddonLocalizer.Services;

public interface IDialogService
{
    Task<bool> ShowConfirmationAsync(string title, string message, string acceptButton = "Yes", string cancelButton = "No");
    Task ShowAlertAsync(string title, string message, string button = "OK");
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
}
