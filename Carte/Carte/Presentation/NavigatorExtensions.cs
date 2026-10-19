namespace Carte.Presentation;

internal static class NavigatorExtensions
{
    // Prefer going back so Explore keeps its page and scroll position; start fresh when there is no history.
    public static async Task ReturnToMenuAsync(this INavigator navigator, object sender)
    {
        var response = await navigator.NavigateBackAsync(sender);
        if (response?.Success != true)
            await navigator.NavigateRouteAsync(sender, "Explore", qualifier: Qualifiers.ClearBackStack);
    }
}
