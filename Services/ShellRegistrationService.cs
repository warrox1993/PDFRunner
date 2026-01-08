using Microsoft.Win32;
using System;
using System.IO;

namespace ConvertPDF.Services
{
    /// <summary>
    /// Handles Windows Explorer Shell Integration via Registry.
    /// Adds/removes "Convert with ConvertPDF" context menu for supported file types.
    /// </summary>
    public class ShellRegistrationService
    {
        private const string AppName = "ConvertPDF";
        private const string MenuText = "Convertir avec ConvertPDF";
        
        private static readonly string[] SupportedExtensions = { ".pdf", ".docx", ".doc", ".png", ".jpg", ".jpeg", ".bmp" };

        public bool IsRegistered { get; private set; }

        public ShellRegistrationService()
        {
            CheckRegistration();
        }

        private void CheckRegistration()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\.pdf\shell\{AppName}");
                IsRegistered = key != null;
            }
            catch
            {
                IsRegistered = false;
            }
        }

        /// <summary>
        /// Registers the app in the Windows context menu for supported file types.
        /// Requires no admin rights (uses HKCU).
        /// </summary>
        public void Register()
        {
            string exePath = Environment.ProcessPath ?? 
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ConvertPDF.exe");

            foreach (string ext in SupportedExtensions)
            {
                try
                {
                    // Create: HKCU\Software\Classes\.ext\shell\ConvertPDF\command
                    string keyPath = $@"Software\Classes\{ext}\shell\{AppName}";
                    
                    using var shellKey = Registry.CurrentUser.CreateSubKey(keyPath);
                    shellKey?.SetValue("", MenuText);
                    shellKey?.SetValue("Icon", $"\"{exePath}\",0");

                    using var commandKey = Registry.CurrentUser.CreateSubKey($@"{keyPath}\command");
                    commandKey?.SetValue("", $"\"{exePath}\" \"%1\"");
                }
                catch
                {
                    // Silently fail for individual extensions
                }
            }

            IsRegistered = true;
        }

        /// <summary>
        /// Removes the app from the Windows context menu.
        /// </summary>
        public void Unregister()
        {
            foreach (string ext in SupportedExtensions)
            {
                try
                {
                    string keyPath = $@"Software\Classes\{ext}\shell\{AppName}";
                    Registry.CurrentUser.DeleteSubKeyTree(keyPath, false);
                }
                catch
                {
                    // Silently fail
                }
            }

            IsRegistered = false;
        }
    }
}
