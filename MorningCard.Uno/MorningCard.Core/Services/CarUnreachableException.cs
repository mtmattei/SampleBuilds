namespace MorningCard.Core.Services;

/// <summary>
/// Raised when a feed has nothing to show at all, as opposed to having stale data to show. The card
/// answers this with its error template; a stale-but-present snapshot is a value, not an error.
/// </summary>
public sealed class CarUnreachableException : Exception
{
    public CarUnreachableException()
        : this("The car could not be reached.")
    {
    }

    public CarUnreachableException(string message)
        : base(message)
    {
    }

    public CarUnreachableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
