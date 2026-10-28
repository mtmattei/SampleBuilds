// Generated from the Carte 2.3 SVG layers and motion catalog. Do not hand-edit.
namespace Carte.Controls;
internal sealed record IllustrationFrame(double Offset, double Value);
internal sealed record IllustrationTrack(string Target, string Property, int Duration, int Delay, IllustrationFrame[] Frames);
internal sealed record IllustrationDefinition(string Xaml, IllustrationTrack[] Tracks);
internal static class IllustrationCatalog
{
    internal static readonly IReadOnlyDictionary<string, IllustrationDefinition> Items = new Dictionary<string, IllustrationDefinition>(StringComparer.Ordinal)
    {
["steak"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Path Data="M8 28c-2-7 4-13 12-13 5 0 7 5 12 5 7 0 11 4 9 10-3 8-10 10-19 8-7-1-12-4-14-10Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M11 28c0-5 4-9 9-9 4 0 7 5 12 5 4 0 6 2 5 5-2 5-8 7-15 5-5-1-10-3-11-6Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Canvas Width="48" Height="48" x:Name="Part_sear" Opacity=".6"><Path Data="m17 24 5 5m2-7 6 7m1-4 4 4" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas><Canvas Width="48" Height="48" x:Name="Part_heat_a" Opacity="0"><Canvas x:Name="Layer_heat_a_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="heat_a_translateY"/></Canvas.RenderTransform><Path Data="M17 15c-4-3 4-4 0-8" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_heat_b" Opacity="0"><Canvas x:Name="Layer_heat_b_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="heat_b_translateY"/></Canvas.RenderTransform><Path Data="M26 13c-4-3 4-4 0-8" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_heat_c" Opacity="0"><Canvas x:Name="Layer_heat_c_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="heat_c_translateY"/></Canvas.RenderTransform><Path Data="M35 18c-4-3 4-4 0-8" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_sizzle_a" Opacity="0"><Path Data="m6 32-2-1m4 6-2 2" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas><Canvas Width="48" Height="48" x:Name="Part_sizzle_b" Opacity="0"><Path Data="m42 30 2-2m-3 9 2 2" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas>
""", [
new("Part_sear", "Opacity", 1600, 0, [new(0.0, 0.6), new(0.5, 1.0), new(1.0, 0.6)]),
new("Part_heat_a", "Opacity", 1450, 0, [new(0.0, 0.0), new(0.18, 0.85), new(0.47, 0.0), new(0.5, 0.0), new(0.68, 0.7), new(1.0, 0.0)]),
new("Layer_heat_a_translateY", "Y", 1450, 0, [new(0.0, 3.0), new(0.18, 0.0), new(0.47, -5.0), new(0.5, 3.0), new(0.68, 0.0), new(1.0, -6.0)]),
new("Part_heat_b", "Opacity", 1600, 100, [new(0.0, 0.0), new(0.18, 0.85), new(0.47, 0.0), new(0.5, 0.0), new(0.68, 0.7), new(1.0, 0.0)]),
new("Layer_heat_b_translateY", "Y", 1600, 100, [new(0.0, 3.0), new(0.18, 0.0), new(0.47, -5.0), new(0.5, 3.0), new(0.68, 0.0), new(1.0, -6.0)]),
new("Part_heat_c", "Opacity", 1400, 230, [new(0.0, 0.0), new(0.18, 0.85), new(0.47, 0.0), new(0.5, 0.0), new(0.68, 0.7), new(1.0, 0.0)]),
new("Layer_heat_c_translateY", "Y", 1400, 230, [new(0.0, 3.0), new(0.18, 0.0), new(0.47, -5.0), new(0.5, 3.0), new(0.68, 0.0), new(1.0, -6.0)]),
new("Part_sizzle_a", "Opacity", 1300, 0, [new(0.0, 0.0), new(0.25, 0.8), new(0.5, 0.0), new(0.75, 0.75), new(1.0, 0.0)]),
new("Part_sizzle_b", "Opacity", 1300, 180, [new(0.0, 0.0), new(0.25, 0.7), new(0.5, 0.0), new(0.75, 0.8), new(1.0, 0.0)])
]),
["oysters"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Canvas Width="48" Height="48" x:Name="Part_lower_shell"><Path Data="M7 30c2-3 5-6 8-6 3-3 9-3 12-2 5-1 8 1 10 3 4 1 6 3 5 6-2 6-7 9-15 9-8 1-15-2-20-10Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M10 32c5 5 10 6 17 5 6 0 10-2 12-6" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas><Canvas Width="48" Height="48" x:Name="Part_oyster_flesh"><Path Data="M17 29c3-4 8-5 13-2 4 0 6 3 3 6-2 3-7 3-11 1-4 0-6-2-5-5Z" Fill="{Binding Foreground}" Opacity=".1"/><Path Data="M17 29c3-4 8-5 13-2 4 0 6 3 3 6-2 3-7 3-11 1-4 0-6-2-5-5Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M22 29c2-2 6-1 7 1" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas><Canvas Width="48" Height="48" x:Name="Part_upper_shell"><Canvas x:Name="Layer_upper_shell_rotate" Width="48" Height="48"><Canvas.RenderTransform><RotateTransform x:Name="upper_shell_rotate" CenterX="8.0" CenterY="30.0"/></Canvas.RenderTransform><Path Data="M7 30c2-3 5-6 8-6 3-3 9-3 12-2 5-1 8 1 10 3 4 1 6 3 5 6-2 6-7 9-15 9-8 1-15-2-20-10Z" Fill="{Binding SurfaceBrush}" Opacity="1"/><Path Data="M7 30c2-3 5-6 8-6 3-3 9-3 12-2 5-1 8 1 10 3 4 1 6 3 5 6-2 6-7 9-15 9-8 1-15-2-20-10Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M10 30c7-6 18-8 28-2M13 31c8-5 16-5 22-1M15 33c7-3 12-3 17-1" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas></Canvas>
""", [
new("Layer_upper_shell_rotate", "Angle", 1700, 0, [new(0.0, 0.0), new(0.34, -38.0), new(0.72, -38.0), new(1.0, 0.0)])
]),
["crudo"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Ellipse Width="36.0" Height="12.0" Canvas.Left="6.0" Canvas.Top="30.0" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M10 39c8 7 23 7 28 0" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M12 31c1-6 4-9 8-7l5 7c-5 3-9 4-13 0ZM24 30c1-6 4-9 8-7l5 7c-5 3-9 4-13 0Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="m16 27 4 4m8-5 4 4" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Canvas Width="48" Height="48" x:Name="Part_citrus"><Canvas x:Name="Layer_citrus_scaleX" Width="48" Height="48"><Canvas.RenderTransform><ScaleTransform x:Name="citrus_scaleX" CenterX="26.0" CenterY="10.0"/></Canvas.RenderTransform><Path Data="M16 10a10 10 0 0 0 20 0ZM19 11a7 7 0 0 0 14 0M26 11v6m-5-6 2 5m8-5-2 5" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_juice_a" Opacity="0"><Canvas x:Name="Layer_juice_a_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="juice_a_translateY"/></Canvas.RenderTransform><Canvas x:Name="Layer_juice_a_scaleX" Width="48" Height="48"><Canvas.RenderTransform><ScaleTransform x:Name="juice_a_scaleX" CenterX="24.0" CenterY="24.0"/></Canvas.RenderTransform><Canvas x:Name="Layer_juice_a_scaleY" Width="48" Height="48"><Canvas.RenderTransform><ScaleTransform x:Name="juice_a_scaleY" CenterX="24.0" CenterY="24.0"/></Canvas.RenderTransform><Path Data="M23 17c0 0-2 3-2 4a2 2 0 0 0 4 0c0-1-2-4-2-4Z" Fill="{Binding Foreground}" Opacity="1"/></Canvas></Canvas></Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_juice_b" Opacity="0"><Canvas x:Name="Layer_juice_b_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="juice_b_translateY"/></Canvas.RenderTransform><Canvas x:Name="Layer_juice_b_scaleX" Width="48" Height="48"><Canvas.RenderTransform><ScaleTransform x:Name="juice_b_scaleX" CenterX="24.0" CenterY="24.0"/></Canvas.RenderTransform><Canvas x:Name="Layer_juice_b_scaleY" Width="48" Height="48"><Canvas.RenderTransform><ScaleTransform x:Name="juice_b_scaleY" CenterX="24.0" CenterY="24.0"/></Canvas.RenderTransform><Path Data="M29 16c0 0-2 3-2 4a2 2 0 0 0 4 0c0-1-2-4-2-4Z" Fill="{Binding Foreground}" Opacity="1"/></Canvas></Canvas></Canvas></Canvas></Canvas>
""", [
new("Layer_citrus_scaleX", "ScaleX", 1400, 0, [new(0.0, 1.0), new(0.28, 0.75), new(0.62, 0.75), new(1.0, 1.0)]),
new("Part_juice_a", "Opacity", 1100, 150, [new(0.0, 0.0), new(0.2, 1.0), new(0.65, 0.9), new(0.8, 0.0), new(1.0, 0.0)]),
new("Layer_juice_a_translateY", "Y", 1100, 150, [new(0.0, 0.0), new(0.2, 1.0), new(0.65, 9.0), new(0.8, 10.0), new(1.0, 10.0)]),
new("Layer_juice_a_scaleX", "ScaleX", 1100, 150, [new(0.0, 0.75), new(0.2, 1.0), new(0.65, 0.8), new(0.8, 0.5), new(1.0, 0.5)]),
new("Layer_juice_a_scaleY", "ScaleY", 1100, 150, [new(0.0, 0.75), new(0.2, 1.0), new(0.65, 0.8), new(0.8, 0.5), new(1.0, 0.5)]),
new("Part_juice_b", "Opacity", 1100, 380, [new(0.0, 0.0), new(0.2, 1.0), new(0.65, 0.9), new(0.8, 0.0), new(1.0, 0.0)]),
new("Layer_juice_b_translateY", "Y", 1100, 380, [new(0.0, 0.0), new(0.2, 1.0), new(0.65, 10.0), new(0.8, 11.0), new(1.0, 11.0)]),
new("Layer_juice_b_scaleX", "ScaleX", 1100, 380, [new(0.0, 0.75), new(0.2, 1.0), new(0.65, 0.8), new(0.8, 0.5), new(1.0, 0.5)]),
new("Layer_juice_b_scaleY", "ScaleY", 1100, 380, [new(0.0, 0.75), new(0.2, 1.0), new(0.65, 0.8), new(0.8, 0.5), new(1.0, 0.5)])
]),
["tomatoes"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Ellipse Width="36.0" Height="12.0" Canvas.Left="6.0" Canvas.Top="30.0" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M10 39c8 7 23 7 28 0" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M12 30a7 7 0 0 1 14 0ZM25 31a7 7 0 0 1 14 0ZM15 29l3-3m4 3-2-3m9 4 3-3m3 3-2-3" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Canvas Width="48" Height="48" x:Name="Part_basil"><Path Data="M19 23c-6-1-8-5-8-10 6-1 11 2 8 10Zm0 0-5-7" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas><Canvas Width="48" Height="48" x:Name="Part_oil_a" Opacity="0"><Canvas x:Name="Layer_oil_a_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="oil_a_translateY"/></Canvas.RenderTransform><Canvas x:Name="Layer_oil_a_scaleX" Width="48" Height="48"><Canvas.RenderTransform><ScaleTransform x:Name="oil_a_scaleX" CenterX="24.0" CenterY="24.0"/></Canvas.RenderTransform><Canvas x:Name="Layer_oil_a_scaleY" Width="48" Height="48"><Canvas.RenderTransform><ScaleTransform x:Name="oil_a_scaleY" CenterX="24.0" CenterY="24.0"/></Canvas.RenderTransform><Path Data="M26 8c0 0-2 3-2 4a2 2 0 0 0 4 0c0-1-2-4-2-4Z" Fill="{Binding Foreground}" Opacity="1"/></Canvas></Canvas></Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_oil_b" Opacity="0"><Canvas x:Name="Layer_oil_b_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="oil_b_translateY"/></Canvas.RenderTransform><Canvas x:Name="Layer_oil_b_scaleX" Width="48" Height="48"><Canvas.RenderTransform><ScaleTransform x:Name="oil_b_scaleX" CenterX="24.0" CenterY="24.0"/></Canvas.RenderTransform><Canvas x:Name="Layer_oil_b_scaleY" Width="48" Height="48"><Canvas.RenderTransform><ScaleTransform x:Name="oil_b_scaleY" CenterX="24.0" CenterY="24.0"/></Canvas.RenderTransform><Path Data="M34 11c0 0-2 3-2 4a2 2 0 0 0 4 0c0-1-2-4-2-4Z" Fill="{Binding Foreground}" Opacity="1"/></Canvas></Canvas></Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_oil_pool" Opacity="0"><Canvas x:Name="Layer_oil_pool_scaleX" Width="48" Height="48"><Canvas.RenderTransform><ScaleTransform x:Name="oil_pool_scaleX" CenterX="29.0" CenterY="34.0"/></Canvas.RenderTransform><Ellipse Width="12.0" Height="3.0" Canvas.Left="23.0" Canvas.Top="32.5" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas></Canvas>
""", [
new("Part_oil_a", "Opacity", 1100, 0, [new(0.0, 0.0), new(0.2, 1.0), new(0.65, 0.9), new(0.8, 0.0), new(1.0, 0.0)]),
new("Layer_oil_a_translateY", "Y", 1100, 0, [new(0.0, 0.0), new(0.2, 1.0), new(0.65, 15.0), new(0.8, 16.0), new(1.0, 16.0)]),
new("Layer_oil_a_scaleX", "ScaleX", 1100, 0, [new(0.0, 0.75), new(0.2, 1.0), new(0.65, 0.8), new(0.8, 0.5), new(1.0, 0.5)]),
new("Layer_oil_a_scaleY", "ScaleY", 1100, 0, [new(0.0, 0.75), new(0.2, 1.0), new(0.65, 0.8), new(0.8, 0.5), new(1.0, 0.5)]),
new("Part_oil_b", "Opacity", 1100, 220, [new(0.0, 0.0), new(0.2, 1.0), new(0.65, 0.9), new(0.8, 0.0), new(1.0, 0.0)]),
new("Layer_oil_b_translateY", "Y", 1100, 220, [new(0.0, 0.0), new(0.2, 1.0), new(0.65, 14.0), new(0.8, 15.0), new(1.0, 15.0)]),
new("Layer_oil_b_scaleX", "ScaleX", 1100, 220, [new(0.0, 0.75), new(0.2, 1.0), new(0.65, 0.8), new(0.8, 0.5), new(1.0, 0.5)]),
new("Layer_oil_b_scaleY", "ScaleY", 1100, 220, [new(0.0, 0.75), new(0.2, 1.0), new(0.65, 0.8), new(0.8, 0.5), new(1.0, 0.5)]),
new("Part_oil_pool", "Opacity", 1500, 0, [new(0.0, 0.0), new(0.5, 0.7), new(1.0, 0.0)]),
new("Layer_oil_pool_scaleX", "ScaleX", 1500, 0, [new(0.0, 0.2), new(0.5, 1.0), new(1.0, 1.15)])
]),
["agnolotti"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Ellipse Width="36.0" Height="12.0" Canvas.Left="6.0" Canvas.Top="30.0" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M10 39c8 7 23 7 28 0" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="m12 28 8-5 7 7-8 5Zm13-4 8-4 6 7-8 5M16 28l4-2 3 3m6-5 3-1 3 3" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Canvas Width="48" Height="48" x:Name="Part_steam_a" Opacity="0"><Canvas x:Name="Layer_steam_a_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="steam_a_translateY"/></Canvas.RenderTransform><Path Data="M18 21c-4-3 4-4 0-8" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_steam_b" Opacity="0"><Canvas x:Name="Layer_steam_b_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="steam_b_translateY"/></Canvas.RenderTransform><Path Data="M29 18c-4-3 4-4 0-8" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_steam_c" Opacity="0"><Canvas x:Name="Layer_steam_c_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="steam_c_translateY"/></Canvas.RenderTransform><Path Data="M35 17c-4-3 4-4 0-8" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas></Canvas>
""", [
new("Part_steam_a", "Opacity", 1700, 0, [new(0.0, 0.0), new(0.18, 0.85), new(0.47, 0.0), new(0.5, 0.0), new(0.68, 0.7), new(1.0, 0.0)]),
new("Layer_steam_a_translateY", "Y", 1700, 0, [new(0.0, 3.0), new(0.18, 0.0), new(0.47, -5.0), new(0.5, 3.0), new(0.68, 0.0), new(1.0, -6.0)]),
new("Part_steam_b", "Opacity", 1550, 160, [new(0.0, 0.0), new(0.18, 0.85), new(0.47, 0.0), new(0.5, 0.0), new(0.68, 0.7), new(1.0, 0.0)]),
new("Layer_steam_b_translateY", "Y", 1550, 160, [new(0.0, 3.0), new(0.18, 0.0), new(0.47, -5.0), new(0.5, 3.0), new(0.68, 0.0), new(1.0, -6.0)]),
new("Part_steam_c", "Opacity", 1600, 330, [new(0.0, 0.0), new(0.18, 0.85), new(0.47, 0.0), new(0.5, 0.0), new(0.68, 0.7), new(1.0, 0.0)]),
new("Layer_steam_c_translateY", "Y", 1600, 330, [new(0.0, 3.0), new(0.18, 0.0), new(0.47, -5.0), new(0.5, 3.0), new(0.68, 0.0), new(1.0, -6.0)])
]),
["burrata"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Ellipse Width="36.0" Height="12.0" Canvas.Left="6.0" Canvas.Top="30.0" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M10 39c8 7 23 7 28 0" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Canvas Width="48" Height="48" x:Name="Part_cream" Opacity="0"><Ellipse Width="14.0" Height="10.0" Canvas.Left="17.0" Canvas.Top="26.0" Fill="{Binding Foreground}" Opacity=".12"/><Ellipse Width="14.0" Height="10.0" Canvas.Left="17.0" Canvas.Top="26.0" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M21 27c-2 4 2 4 1 7m4-7c2 3-2 4 0 6" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas><Canvas Width="48" Height="48" x:Name="Part_cheese_left"><Canvas x:Name="Layer_cheese_left_translateX" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="cheese_left_translateX"/></Canvas.RenderTransform><Path Data="M24 16c-6-4-14 3-14 11 0 7 6 11 14 9Z" Fill="{Binding SurfaceBrush}" Opacity="1"/><Path Data="M24 16c-6-4-14 3-14 11 0 7 6 11 14 9Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M17 19c-3 2-4 5-3 8" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_cheese_right"><Canvas x:Name="Layer_cheese_right_translateX" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="cheese_right_translateX"/></Canvas.RenderTransform><Path Data="M24 16c6-4 14 3 14 11 0 7-6 11-14 9Z" Fill="{Binding SurfaceBrush}" Opacity="1"/><Path Data="M24 16c6-4 14 3 14 11 0 7-6 11-14 9Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas><Path Data="M20 15c-2-4 2-7 4-5 3-2 6 1 4 5" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas>
""", [
new("Part_cream", "Opacity", 1600, 0, [new(0.0, 0.0), new(0.25, 1.0), new(0.8, 1.0), new(1.0, 0.0)]),
new("Layer_cheese_left_translateX", "X", 1600, 0, [new(0.0, 0.0), new(0.38, -3.0), new(0.75, -3.0), new(1.0, 0.0)]),
new("Layer_cheese_right_translateX", "X", 1600, 0, [new(0.0, 0.0), new(0.38, 3.0), new(0.75, 3.0), new(1.0, 0.0)])
]),
["albarino"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Canvas x:Name="Part_liquid" Width="48" Height="48">
<Canvas.RenderTransform><TranslateTransform x:Name="liquid_translateY"/></Canvas.RenderTransform>
<Canvas.Clip><RectangleGeometry Rect="0,23.0,48,25.0"/></Canvas.Clip>
<Canvas x:Name="Layer_liquid_counter" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="liquid_counter"/></Canvas.RenderTransform>
<Path Data="M15 8h18l2 12c1 7-4 13-11 13S12 27 13 20Z" Fill="{Binding Foreground}" Opacity="0.24"/>
</Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_glass"><Path Data="M15 8h18l2 12c1 7-4 13-11 13S12 27 13 20Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M24 33v9m-7 0h14" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas>
""", [
new("Part_liquid", "Y", 1600, 0, [new(0.0, 13.0), new(0.57, 0.0), new(0.7, 0.7), new(0.83, 0.0), new(1.0, 0.0)]),
new("Layer_liquid_counter", "Y", 1600, 0, [new(0.0, -13.0), new(0.57, -0.0), new(0.7, -0.7), new(0.83, -0.0), new(1.0, -0.0)])
]),
["syrah"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Canvas x:Name="Part_liquid" Width="48" Height="48">
<Canvas.RenderTransform><TranslateTransform x:Name="liquid_translateY"/></Canvas.RenderTransform>
<Canvas.Clip><RectangleGeometry Rect="0,24.0,48,24.0"/></Canvas.Clip>
<Canvas x:Name="Layer_liquid_counter" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="liquid_counter"/></Canvas.RenderTransform>
<Path Data="M15 8h18c0 7 5 9 5 15 0 7-6 11-14 11S10 30 10 23c0-6 5-8 5-15Z" Fill="{Binding Foreground}" Opacity="0.24"/>
</Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_glass"><Path Data="M15 8h18c0 7 5 9 5 15 0 7-6 11-14 11S10 30 10 23c0-6 5-8 5-15Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M24 34v8m-7 0h14" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas>
""", [
new("Part_liquid", "Y", 1950, 0, [new(0.0, 13.0), new(0.57, 0.0), new(0.7, 0.7), new(0.83, 0.0), new(1.0, 0.0)]),
new("Layer_liquid_counter", "Y", 1950, 0, [new(0.0, -13.0), new(0.57, -0.0), new(0.7, -0.7), new(0.83, -0.0), new(1.0, -0.0)])
]),
["muscadet"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Canvas x:Name="Part_liquid" Width="48" Height="48">
<Canvas.RenderTransform><TranslateTransform x:Name="liquid_translateY"/></Canvas.RenderTransform>
<Canvas.Clip><RectangleGeometry Rect="0,21.0,48,27.0"/></Canvas.Clip>
<Canvas x:Name="Layer_liquid_counter" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="liquid_counter"/></Canvas.RenderTransform>
<Path Data="M17 7h14l-1 18c0 5-3 8-6 8s-6-3-6-8Z" Fill="{Binding Foreground}" Opacity="0.24"/>
</Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_glass"><Path Data="M17 7h14l-1 18c0 5-3 8-6 8s-6-3-6-8Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M24 33v9m-6 0h12" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas>
""", [
new("Part_liquid", "Y", 1500, 0, [new(0.0, 15.0), new(0.57, 0.0), new(0.7, 0.7), new(0.83, 0.0), new(1.0, 0.0)]),
new("Layer_liquid_counter", "Y", 1500, 0, [new(0.0, -15.0), new(0.57, -0.0), new(0.7, -0.7), new(0.83, -0.0), new(1.0, -0.0)])
]),
["riesling"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Canvas x:Name="Part_liquid" Width="48" Height="48">
<Canvas.RenderTransform><TranslateTransform x:Name="liquid_translateY"/></Canvas.RenderTransform>
<Canvas.Clip><RectangleGeometry Rect="0,20.0,48,28.0"/></Canvas.Clip>
<Canvas x:Name="Layer_liquid_counter" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="liquid_counter"/></Canvas.RenderTransform>
<Path Data="M15 8h18l2 12c1 7-4 13-11 13S12 27 13 20Z" Fill="{Binding Foreground}" Opacity="0.24"/>
</Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_glass"><Path Data="M15 8h18l2 12c1 7-4 13-11 13S12 27 13 20Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M24 33v9m-7 0h14" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas>
""", [
new("Part_liquid", "Y", 1750, 0, [new(0.0, 16.0), new(0.57, 0.0), new(0.7, 0.7), new(0.83, 0.0), new(1.0, 0.0)]),
new("Layer_liquid_counter", "Y", 1750, 0, [new(0.0, -16.0), new(0.57, -0.0), new(0.7, -0.7), new(0.83, -0.0), new(1.0, -0.0)])
]),
["chardonnay"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Canvas x:Name="Part_liquid" Width="48" Height="48">
<Canvas.RenderTransform><TranslateTransform x:Name="liquid_translateY"/></Canvas.RenderTransform>
<Canvas.Clip><RectangleGeometry Rect="0,21.0,48,27.0"/></Canvas.Clip>
<Canvas x:Name="Layer_liquid_counter" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="liquid_counter"/></Canvas.RenderTransform>
<Path Data="M15 8h18c0 7 5 9 5 15 0 7-6 11-14 11S10 30 10 23c0-6 5-8 5-15Z" Fill="{Binding Foreground}" Opacity="0.24"/>
</Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_glass"><Path Data="M15 8h18c0 7 5 9 5 15 0 7-6 11-14 11S10 30 10 23c0-6 5-8 5-15Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M24 34v8m-7 0h14" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas>
""", [
new("Part_liquid", "Y", 1850, 0, [new(0.0, 16.0), new(0.57, 0.0), new(0.7, 0.7), new(0.83, 0.0), new(1.0, 0.0)]),
new("Layer_liquid_counter", "Y", 1850, 0, [new(0.0, -16.0), new(0.57, -0.0), new(0.7, -0.7), new(0.83, -0.0), new(1.0, -0.0)])
]),
["gamay"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Canvas x:Name="Part_liquid" Width="48" Height="48">
<Canvas.RenderTransform><TranslateTransform x:Name="liquid_translateY"/></Canvas.RenderTransform>
<Canvas.Clip><RectangleGeometry Rect="0,25.0,48,23.0"/></Canvas.Clip>
<Canvas x:Name="Layer_liquid_counter" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="liquid_counter"/></Canvas.RenderTransform>
<Path Data="M15 8h18l2 12c1 7-4 13-11 13S12 27 13 20Z" Fill="{Binding Foreground}" Opacity="0.24"/>
</Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_glass"><Path Data="M15 8h18l2 12c1 7-4 13-11 13S12 27 13 20Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M24 33v9m-7 0h14" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas>
""", [
new("Part_liquid", "Y", 1650, 0, [new(0.0, 11.0), new(0.57, 0.0), new(0.7, 0.7), new(0.83, 0.0), new(1.0, 0.0)]),
new("Layer_liquid_counter", "Y", 1650, 0, [new(0.0, -11.0), new(0.57, -0.0), new(0.7, -0.7), new(0.83, -0.0), new(1.0, -0.0)])
]),
["spritz"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Canvas x:Name="Part_liquid" Width="48" Height="48">
<Canvas.RenderTransform><TranslateTransform x:Name="liquid_translateY"/></Canvas.RenderTransform>
<Canvas.Clip><RectangleGeometry Rect="0,21.0,48,27.0"/></Canvas.Clip>
<Canvas x:Name="Layer_liquid_counter" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="liquid_counter"/></Canvas.RenderTransform>
<Path Data="M15 8h18c0 7 5 9 5 15 0 7-6 11-14 11S10 30 10 23c0-6 5-8 5-15Z" Fill="{Binding Foreground}" Opacity="0.24"/>
</Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_glass"><Path Data="M15 8h18c0 7 5 9 5 15 0 7-6 11-14 11S10 30 10 23c0-6 5-8 5-15Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M24 34v8m-7 0h14" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas><Canvas Width="48" Height="48" x:Name="Part_orange"><Path Data="M33 9a7 7 0 1 1 7 7M35 10l4 3m-2-5 1 3" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas><Canvas Width="48" Height="48" x:Name="Part_bubble_a" Opacity="0"><Canvas x:Name="Layer_bubble_a_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="bubble_a_translateY"/></Canvas.RenderTransform><Ellipse Width="2.2" Height="2.2" Canvas.Left="17.9" Canvas.Top="27.9" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_bubble_b" Opacity="0"><Canvas x:Name="Layer_bubble_b_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="bubble_b_translateY"/></Canvas.RenderTransform><Ellipse Width="2.2" Height="2.2" Canvas.Left="25.9" Canvas.Top="26.9" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas></Canvas>
""", [
new("Part_liquid", "Y", 1700, 0, [new(0.0, 16.0), new(0.57, 0.0), new(0.7, 0.7), new(0.83, 0.0), new(1.0, 0.0)]),
new("Layer_liquid_counter", "Y", 1700, 0, [new(0.0, -16.0), new(0.57, -0.0), new(0.7, -0.7), new(0.83, -0.0), new(1.0, -0.0)]),
new("Part_bubble_a", "Opacity", 1000, 450, [new(0.0, 0.0), new(0.22, 0.9), new(0.8, 0.65), new(1.0, 0.0)]),
new("Layer_bubble_a_translateY", "Y", 1000, 450, [new(0.0, 0.0), new(0.22, -2.0), new(0.8, -10.0), new(1.0, -12.0)]),
new("Part_bubble_b", "Opacity", 1000, 670, [new(0.0, 0.0), new(0.22, 0.9), new(0.8, 0.65), new(1.0, 0.0)]),
new("Layer_bubble_b_translateY", "Y", 1000, 670, [new(0.0, 0.0), new(0.22, -2.0), new(0.8, -12.0), new(1.0, -14.0)])
]),
["negroni"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Canvas x:Name="Part_liquid" Width="48" Height="48">
<Canvas.RenderTransform><TranslateTransform x:Name="liquid_translateY"/></Canvas.RenderTransform>
<Canvas.Clip><RectangleGeometry Rect="0,25.0,48,23.0"/></Canvas.Clip>
<Canvas x:Name="Layer_liquid_counter" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="liquid_counter"/></Canvas.RenderTransform>
<Path Data="M12 15h24l-2 24H14Z" Fill="{Binding Foreground}" Opacity="0.24"/>
</Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_glass"><Path Data="M12 15h24l-2 24H14Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M14 35h20" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas><Canvas Width="48" Height="48" x:Name="Part_ice"><Canvas x:Name="Layer_ice_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="ice_translateY"/></Canvas.RenderTransform><Canvas x:Name="Layer_ice_rotate" Width="48" Height="48"><Canvas.RenderTransform><RotateTransform x:Name="ice_rotate" CenterX="24.0" CenterY="27.0"/></Canvas.RenderTransform><Path Data="m20 21 9 2-2 10-9-2Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas></Canvas><Path Data="M29 17c0-6 7-8 10-3m-10 3 10-3" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas>
""", [
new("Part_liquid", "Y", 1700, 0, [new(0.0, 17.0), new(0.57, 0.0), new(0.7, 0.7), new(0.83, 0.0), new(1.0, 0.0)]),
new("Layer_liquid_counter", "Y", 1700, 0, [new(0.0, -17.0), new(0.57, -0.0), new(0.7, -0.7), new(0.83, -0.0), new(1.0, -0.0)]),
new("Layer_ice_translateY", "Y", 1800, 0, [new(0.0, 2.0), new(0.5, -2.0), new(0.8, 0.5), new(1.0, 0.0)]),
new("Layer_ice_rotate", "Angle", 1800, 0, [new(0.0, -5.0), new(0.5, 4.0), new(0.8, -1.0), new(1.0, 0.0)])
]),
["paloma"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Canvas x:Name="Part_liquid" Width="48" Height="48">
<Canvas.RenderTransform><TranslateTransform x:Name="liquid_translateY"/></Canvas.RenderTransform>
<Canvas.Clip><RectangleGeometry Rect="0,19.0,48,29.0"/></Canvas.Clip>
<Canvas x:Name="Layer_liquid_counter" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="liquid_counter"/></Canvas.RenderTransform>
<Path Data="M15 8h18l-1 33H16Z" Fill="{Binding Foreground}" Opacity="0.24"/>
</Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_glass"><Path Data="M15 8h18l-1 33H16Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M16 37h16" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas><Canvas Width="48" Height="48" x:Name="Part_grapefruit"><Path Data="M31 13a8 8 0 1 0-7-7m0 0 7 7m-4-6 2 1m-6 2 3 1" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas><Canvas Width="48" Height="48" x:Name="Part_bubble_a" Opacity="0"><Canvas x:Name="Layer_bubble_a_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="bubble_a_translateY"/></Canvas.RenderTransform><Ellipse Width="2.2" Height="2.2" Canvas.Left="19.9" Canvas.Top="32.9" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_bubble_b" Opacity="0"><Canvas x:Name="Layer_bubble_b_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="bubble_b_translateY"/></Canvas.RenderTransform><Ellipse Width="2.2" Height="2.2" Canvas.Left="26.9" Canvas.Top="27.9" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas></Canvas>
""", [
new("Part_liquid", "Y", 1700, 0, [new(0.0, 25.0), new(0.57, 0.0), new(0.7, 0.7), new(0.83, 0.0), new(1.0, 0.0)]),
new("Layer_liquid_counter", "Y", 1700, 0, [new(0.0, -25.0), new(0.57, -0.0), new(0.7, -0.7), new(0.83, -0.0), new(1.0, -0.0)]),
new("Part_bubble_a", "Opacity", 1000, 500, [new(0.0, 0.0), new(0.22, 0.9), new(0.8, 0.65), new(1.0, 0.0)]),
new("Layer_bubble_a_translateY", "Y", 1000, 500, [new(0.0, 0.0), new(0.22, -2.0), new(0.8, -16.0), new(1.0, -18.0)]),
new("Part_bubble_b", "Opacity", 1000, 800, [new(0.0, 0.0), new(0.22, 0.9), new(0.8, 0.65), new(1.0, 0.0)]),
new("Layer_bubble_b_translateY", "Y", 1000, 800, [new(0.0, 0.0), new(0.22, -2.0), new(0.8, -10.0), new(1.0, -12.0)])
]),
["martinez"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Canvas x:Name="Part_liquid" Width="48" Height="48">
<Canvas.RenderTransform><TranslateTransform x:Name="liquid_translateY"/></Canvas.RenderTransform>
<Canvas.Clip><RectangleGeometry Rect="0,19.0,48,29.0"/></Canvas.Clip>
<Canvas x:Name="Layer_liquid_counter" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="liquid_counter"/></Canvas.RenderTransform>
<Path Data="M7 10h34L24 31Z" Fill="{Binding Foreground}" Opacity="0.24"/>
</Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_glass"><Path Data="M7 10h34L24 31Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M24 31v11m-8 0h16" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas><Canvas Width="48" Height="48" x:Name="Part_cherry"><Canvas x:Name="Layer_cherry_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="cherry_translateY"/></Canvas.RenderTransform><Ellipse Width="4.4" Height="4.4" Canvas.Left="21.8" Canvas.Top="19.8" Fill="{Binding Foreground}" Opacity=".18"/><Ellipse Width="4.4" Height="4.4" Canvas.Left="21.8" Canvas.Top="19.8" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M24 20c1-3 4-4 5-5" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas></Canvas>
""", [
new("Part_liquid", "Y", 1700, 0, [new(0.0, 15.0), new(0.57, 0.0), new(0.7, 0.7), new(0.83, 0.0), new(1.0, 0.0)]),
new("Layer_liquid_counter", "Y", 1700, 0, [new(0.0, -15.0), new(0.57, -0.0), new(0.7, -0.7), new(0.83, -0.0), new(1.0, -0.0)]),
new("Part_cherry", "Opacity", 1800, 0, [new(0.0, 0.0), new(0.3, 0.0), new(0.44, 1.0), new(0.68, 1.0), new(0.85, 1.0), new(1.0, 1.0)]),
new("Layer_cherry_translateY", "Y", 1800, 0, [new(0.0, -12.0), new(0.3, -12.0), new(0.44, -9.0), new(0.68, 1.0), new(0.85, -0.6), new(1.0, 0.0)])
]),
["americano"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Canvas x:Name="Part_liquid" Width="48" Height="48">
<Canvas.RenderTransform><TranslateTransform x:Name="liquid_translateY"/></Canvas.RenderTransform>
<Canvas.Clip><RectangleGeometry Rect="0,23.0,48,25.0"/></Canvas.Clip>
<Canvas x:Name="Layer_liquid_counter" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="liquid_counter"/></Canvas.RenderTransform>
<Path Data="M15 8h18l-1 33H16Z" Fill="{Binding Foreground}" Opacity="0.24"/>
</Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_glass"><Path Data="M15 8h18l-1 33H16Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M16 37h16" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas><Path Data="m27 19 8-15" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Canvas Width="48" Height="48" x:Name="Part_bubble_a" Opacity="0"><Canvas x:Name="Layer_bubble_a_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="bubble_a_translateY"/></Canvas.RenderTransform><Ellipse Width="2.2" Height="2.2" Canvas.Left="18.9" Canvas.Top="32.9" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_bubble_b" Opacity="0"><Canvas x:Name="Layer_bubble_b_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="bubble_b_translateY"/></Canvas.RenderTransform><Ellipse Width="2.2" Height="2.2" Canvas.Left="24.9" Canvas.Top="30.9" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_bubble_c" Opacity="0"><Canvas x:Name="Layer_bubble_c_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="bubble_c_translateY"/></Canvas.RenderTransform><Ellipse Width="2.2" Height="2.2" Canvas.Left="27.9" Canvas.Top="34.9" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas></Canvas>
""", [
new("Part_liquid", "Y", 1700, 0, [new(0.0, 21.0), new(0.57, 0.0), new(0.7, 0.7), new(0.83, 0.0), new(1.0, 0.0)]),
new("Layer_liquid_counter", "Y", 1700, 0, [new(0.0, -21.0), new(0.57, -0.0), new(0.7, -0.7), new(0.83, -0.0), new(1.0, -0.0)]),
new("Part_bubble_a", "Opacity", 1000, 450, [new(0.0, 0.0), new(0.22, 0.9), new(0.8, 0.65), new(1.0, 0.0)]),
new("Layer_bubble_a_translateY", "Y", 1000, 450, [new(0.0, 0.0), new(0.22, -2.0), new(0.8, -12.0), new(1.0, -14.0)]),
new("Part_bubble_b", "Opacity", 1000, 650, [new(0.0, 0.0), new(0.22, 0.9), new(0.8, 0.65), new(1.0, 0.0)]),
new("Layer_bubble_b_translateY", "Y", 1000, 650, [new(0.0, 0.0), new(0.22, -2.0), new(0.8, -14.0), new(1.0, -16.0)]),
new("Part_bubble_c", "Opacity", 1000, 900, [new(0.0, 0.0), new(0.22, 0.9), new(0.8, 0.65), new(1.0, 0.0)]),
new("Layer_bubble_c_translateY", "Y", 1000, 900, [new(0.0, 0.0), new(0.22, -2.0), new(0.8, -12.0), new(1.0, -14.0)])
]),
["sour"] = new("""
<Canvas xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" Width="48" Height="48" IsHitTestVisible="False"><Canvas x:Name="Part_liquid" Width="48" Height="48">
<Canvas.RenderTransform><TranslateTransform x:Name="liquid_translateY"/></Canvas.RenderTransform>
<Canvas.Clip><RectangleGeometry Rect="0,23.0,48,25.0"/></Canvas.Clip>
<Canvas x:Name="Layer_liquid_counter" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="liquid_counter"/></Canvas.RenderTransform>
<Path Data="M10 15h28c-1 10-6 15-14 15s-13-5-14-15Z" Fill="{Binding Foreground}" Opacity="0.24"/>
</Canvas></Canvas><Canvas Width="48" Height="48" x:Name="Part_glass"><Path Data="M10 15h28c-1 10-6 15-14 15s-13-5-14-15Z" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Path Data="M24 30v12m-7 0h14" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas><Canvas Width="48" Height="48" x:Name="Part_foam"><Canvas x:Name="Layer_foam_translateY" Width="48" Height="48"><Canvas.RenderTransform><TranslateTransform x:Name="foam_translateY"/></Canvas.RenderTransform><Path Data="M12 20q3-4 6-1 3-4 6-1 3-4 6-1 3-3 6 0" Fill="{Binding SurfaceBrush}" Opacity="1"/><Path Data="M12 20q3-4 6-1 3-4 6-1 3-4 6-1 3-3 6 0" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas></Canvas><Path Data="M30 13c0-5 5-6 7-3" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/><Ellipse Width="4.0" Height="4.0" Canvas.Left="28.0" Canvas.Top="12.0" Stroke="{Binding Foreground}" StrokeThickness="1.6" StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round"/></Canvas>
""", [
new("Part_liquid", "Y", 1700, 0, [new(0.0, 10.0), new(0.57, 0.0), new(0.7, 0.7), new(0.83, 0.0), new(1.0, 0.0)]),
new("Layer_liquid_counter", "Y", 1700, 0, [new(0.0, -10.0), new(0.57, -0.0), new(0.7, -0.7), new(0.83, -0.0), new(1.0, -0.0)]),
new("Part_foam", "Opacity", 1750, 0, [new(0.0, 0.0), new(0.4, 0.7), new(0.62, 1.0), new(1.0, 1.0)]),
new("Layer_foam_translateY", "Y", 1750, 0, [new(0.0, 7.0), new(0.4, 2.0), new(0.62, -1.0), new(1.0, 0.0)])
])
    };
}
