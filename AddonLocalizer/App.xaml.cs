using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace AddonLocalizer
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            
            // Add global exception handlers
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Debug.WriteLine($"[App] Unhandled exception: {ex.Message}");
                Debug.WriteLine($"[App] Exception type: {ex.GetType().Name}");
                Debug.WriteLine($"[App] Stack trace: {ex.StackTrace}");
            }
        }

        private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            Debug.WriteLine($"[App] Unobserved task exception: {e.Exception.Message}");
            Debug.WriteLine($"[App] Exception type: {e.Exception.GetType().Name}");
            e.SetObserved(); // Prevent app crash
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}