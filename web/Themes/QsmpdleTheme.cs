using MudBlazor;

namespace QSMPDLE.Web.Themes;

public static class QsmpdleTheme
{
    public static readonly MudTheme Theme = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#16BAFF",
            Secondary = "#7cf1c4",

            Success = "#2FBB36",
            Warning = "#FFA056",
            Error = "#CC284C",

            Background = "#010a1e",
            Surface = "#0A2C5E",

            TextPrimary = "#FAFBFC",
            TextSecondary = "#CACBCC",

            AppbarBackground = "#0A2C5E",
            DrawerBackground = "#010a1e",

            ActionDefault = "#FAFBFC",
            ActionDisabled = "#666666",

            LinesDefault = "rgba(250,251,252,0.12)"
        }
    };
}
