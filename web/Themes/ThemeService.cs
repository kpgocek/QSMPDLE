using MudBlazor;

namespace QSMPDLE.Web.Themes;

public class ThemeService
{
    public MudTheme CurrentTheme { get; set; } = QsmpdleTheme.Theme;
    public bool IsDarkMode { get; set; } = true;
}
