using MudBlazor;

namespace QSMPDLE.Web.Themes;

public static class QsmpdleTheme
{
    private static readonly string[] BodyFont = ["Nunito Sans", "Helvetica", "Arial", "sans-serif"];

    public static readonly MudTheme Theme = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#16BAFF",
            Secondary = "#7cf1c4",

            Success = "#2FBB36",
            Warning = "#FFA056",
            Error = "#CC284C",

            // White on the bright fills is ~2–2.5:1; navy clears 7:1. Error stays white (5.3:1).
            PrimaryContrastText = "#010a1e",
            SuccessContrastText = "#010a1e",
            WarningContrastText = "#010a1e",

            Background = "#010a1e",
            Surface = "#0A2C5E",

            TextPrimary = "#FAFBFC",
            TextSecondary = "#CACBCC",

            AppbarBackground = "#0A2C5E",
            DrawerBackground = "#010a1e",

            ActionDefault = "#FAFBFC",
            ActionDisabled = "#666666",

            LinesDefault = "rgba(250,251,252,0.12)"
        },

        // Every MudBlazor variant defaults to Roboto on its own, so each one is pointed at the body face.
        Typography =
        {
            Default = { FontFamily = BodyFont },
            H1 = { FontFamily = BodyFont },
            H2 = { FontFamily = BodyFont },
            H3 = { FontFamily = BodyFont },
            H4 = { FontFamily = BodyFont },
            H5 = { FontFamily = BodyFont },
            H6 = { FontFamily = BodyFont },
            Subtitle1 = { FontFamily = BodyFont },
            Subtitle2 = { FontFamily = BodyFont },
            Body1 = { FontFamily = BodyFont },
            Body2 = { FontFamily = BodyFont },
            Button = { FontFamily = BodyFont },
            Caption = { FontFamily = BodyFont },
            Overline = { FontFamily = BodyFont }
        }
    };

    // Declared after Theme: static fields initialize in textual order.
    public static readonly string[] ChartPalette =
    [
        Theme.PaletteDark.Primary.ToString(),
        Theme.PaletteDark.Secondary.ToString(),
        Theme.PaletteDark.Success.ToString(),
        Theme.PaletteDark.Warning.ToString(),
        Theme.PaletteDark.PrimaryLighten,
        Theme.PaletteDark.SuccessLighten,
        Theme.PaletteDark.WarningLighten,
        Theme.PaletteDark.SecondaryLighten,
        Theme.PaletteDark.PrimaryDarken,
        Theme.PaletteDark.SuccessDarken,
        Theme.PaletteDark.WarningDarken,
        Theme.PaletteDark.SecondaryDarken
    ];
}
