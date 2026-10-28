namespace Carte.Core;

// Implement these behind dependency injection. No camera, OCR or model provider is included.
public sealed record MenuPhoto(string LocalReference, string ContentType);
public sealed record ExtractedLine(string RawText, decimal? ParsedPrice, bool NeedsReview);
public sealed record ExtractionResult(IReadOnlyList<ExtractedLine> Lines, string? Currency);

public interface IMenuCaptureService
{
    Task<MenuPhoto?> CaptureAsync(CancellationToken cancellationToken);
    Task<MenuPhoto?> PickPhotoAsync(CancellationToken cancellationToken);
}

public interface IMenuExtractionService
{
    Task<ExtractionResult> ExtractAsync(IReadOnlyList<MenuPhoto> pages, CancellationToken cancellationToken);
}

public interface ITasteAnalysisService
{
    // Input is a user-reviewed menu. Output must pass schema + domain validation.
    Task<MenuDocument> AnalyzeAsync(MenuDocument reviewedMenu, CancellationToken cancellationToken);
}

public interface IMenuStore
{
    Task SaveMenuAsync(MenuDocument menu, CancellationToken cancellationToken);
    Task<MenuDocument?> LoadMenuAsync(string menuId, CancellationToken cancellationToken);
    Task SaveShortlistAsync(string menuId, IReadOnlyCollection<string> itemIds, CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> LoadShortlistAsync(string menuId, CancellationToken cancellationToken);
}
