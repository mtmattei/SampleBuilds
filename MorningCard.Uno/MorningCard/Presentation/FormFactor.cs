namespace MorningCard.Presentation;

/// <summary>The device the one APK is running on.</summary>
public enum FormFactor
{
    Phone,
    Television,
    Watch,
}

/// <summary>
/// Read once at startup: a form factor is a fact about the device, not state that changes. Android
/// answers through <c>UiModeManager.CurrentModeType</c>; desktop is a phone unless DEBUG's
/// <c>MC_FORM=tv|watch</c> asks for another page, which lets the App MCP check TV and watch layouts
/// without an emulator.
/// </summary>
public static class FormFactors
{
    public static FormFactor Current { get; } = Detect();

    private static FormFactor Detect()
    {
#if DEBUG
        switch (Environment.GetEnvironmentVariable("MC_FORM")?.ToLowerInvariant())
        {
            case "tv":
                return FormFactor.Television;
            case "watch":
                return FormFactor.Watch;
        }
#endif

#if __ANDROID__
        var modes = (Android.App.UiModeManager?)Android.App.Application.Context.GetSystemService(Android.Content.Context.UiModeService);

        return modes?.CurrentModeType switch
        {
            Android.Content.Res.UiMode.TypeTelevision => FormFactor.Television,
            Android.Content.Res.UiMode.TypeWatch => FormFactor.Watch,
            _ => FormFactor.Phone,
        };
#else
        return FormFactor.Phone;
#endif
    }
}
