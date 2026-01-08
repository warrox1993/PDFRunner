using MaterialDesignThemes.Wpf;
using System.Windows.Media;
using Color = System.Windows.Media.Color;

namespace ConvertPDF.Services
{
    /// <summary>
    /// Centralized Theme Service for Dark Mode and Color management.
    /// Uses Material Design's PaletteHelper for instant theme switching.
    /// </summary>
    public class ThemeService
    {
        private readonly PaletteHelper _paletteHelper = new();

        public bool IsDarkMode { get; private set; }

        public void ApplyTheme(bool isDark)
        {
            IsDarkMode = isDark;
            Theme theme = _paletteHelper.GetTheme();
            theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
            _paletteHelper.SetTheme(theme);
        }

        public void SetPrimaryColor(Color color)
        {
            Theme theme = _paletteHelper.GetTheme();
            theme.SetPrimaryColor(color);
            _paletteHelper.SetTheme(theme);
        }

        public void SetSecondaryColor(Color color)
        {
            Theme theme = _paletteHelper.GetTheme();
            theme.SetSecondaryColor(color);
            _paletteHelper.SetTheme(theme);
        }

        public void ToggleTheme()
        {
            ApplyTheme(!IsDarkMode);
        }
    }
}
