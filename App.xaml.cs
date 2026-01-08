using System;
using System.Configuration;
using System.Data;
using System.Windows;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ConvertPDF.Services;
using ConvertPDF.ViewModels;
using ConvertPDF.Views;
using ConvertPDF.Models; // For Interfaces if any

namespace ConvertPDF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        var logPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ConvertPDF_startup.log");
        
        try
        {
            System.IO.File.WriteAllText(logPath, $"[{DateTime.Now}] App starting...\n");
            
            // Global Exception Handling
            bool isCrashing = false;
            this.DispatcherUnhandledException += (sender, args) =>
            {
                if (isCrashing) return;
                isCrashing = true;

                var crashMsg = $"[{DateTime.Now}] UNHANDLED CRASH!\nException: {args.Exception.GetType().Name}\nMessage: {args.Exception.Message}\nStack: {args.Exception.StackTrace}";
                System.IO.File.AppendAllText(logPath, crashMsg);
                
                System.Windows.MessageBox.Show($"Fatal Error:\n{args.Exception.Message}\n\nL'application va se fermer.", "Crash", MessageBoxButton.OK, MessageBoxImage.Error);
                
                args.Handled = true; 
                System.Environment.Exit(1);
            };

            var services = new ServiceCollection();

            // Services
            services.AddSingleton<ProfileManager>();
            services.AddSingleton<LanguagePackManager>();
            
            // Core Converters
            services.AddSingleton<PdfToWordConverter>();
            services.AddSingleton<PdfToExcelConverter>();
            services.AddSingleton<PdfToHtmlConverter>();
            services.AddSingleton<WordToPdfConverter>();
            services.AddSingleton<ImageToPdfConverter>();
            services.AddSingleton<PdfMergeService>();
            services.AddSingleton<PdfPageManipulator>();
            services.AddSingleton<PdfCompressionService>();
            // services.AddSingleton<PdfImageExtractor>(); // Phase Q - Not implemented yet

            // Audio/Video Services (Phase A/V)
            services.AddSingleton<WhisperTranscriptionService>();
            services.AddSingleton<YouTubeDownloadService>();
            services.AddSingleton<MediaConversionService>();

            // V2.0 Services (Phase 7-9)
            services.AddSingleton<ThemeService>();
            services.AddSingleton<SystemHealthService>();
            services.AddSingleton<ConflictResolver>();
            services.AddSingleton<ConversionHistoryService>();
            services.AddSingleton<SmartOcrDetector>();
            services.AddSingleton<ShellRegistrationService>();
            services.AddSingleton<TranslationService>();

            // ViewModels
            services.AddTransient<MainWindowViewModel>();
            services.AddTransient<PdfToolsViewModel>();
            services.AddTransient<AudioVideoStudioViewModel>();

            // Views
            services.AddTransient<MainWindow>();
            services.AddTransient<PdfToolsWindow>();
            services.AddTransient<AudioVideoStudioWindow>();

            Services = services.BuildServiceProvider();

            // Launch Main Window
            var mainWindow = Services.GetRequiredService<MainWindow>();
            var viewModel = Services.GetRequiredService<MainWindowViewModel>();
            mainWindow.DataContext = viewModel;
            mainWindow.Show();

            // Handle command-line arguments (files passed from Explorer context menu)
            if (e.Args.Length > 0)
            {
                var validFiles = e.Args.Where(arg => System.IO.File.Exists(arg)).ToArray();
                if (validFiles.Any())
                {
                    // Use dispatcher to ensure UI is ready
                    mainWindow.Dispatcher.InvokeAsync(async () =>
                    {
                        await viewModel.OnFilesDropped(validFiles);
                    });
                }
            }

            base.OnStartup(e);
            System.IO.File.AppendAllText(logPath, $"[{DateTime.Now}] App started successfully\n");
        }
        catch (Exception ex)
        {
            var errorMsg = $"[{DateTime.Now}] CRASH!\n\nException: {ex.GetType().Name}\nMessage: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}\n\nInner Exception: {ex.InnerException}\n";
            System.IO.File.AppendAllText(logPath, errorMsg);
            System.Windows.MessageBox.Show($"Crash log saved to:\n{logPath}\n\nError: {ex.Message}", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            throw;
        }
    }
}

