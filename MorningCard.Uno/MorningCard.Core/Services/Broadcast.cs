using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace MorningCard.Core.Services;

/// <summary>
/// A last-value-plus-changes stream for many readers. Each subscriber gets the current value on
/// arrival and every later publish, which is the shape <c>Feed.AsyncEnumerable</c> expects.
/// </summary>
internal sealed class Broadcast<T>
{
    private readonly object _gate = new();
    private readonly List<Channel<T>> _subscribers = new();
    private T _current;

    public Broadcast(T initial) => _current = initial;

    public T Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void Publish(T value)
    {
        lock (_gate)
        {
            _current = value;
            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(value);
            }
        }
    }

    /// <summary>
    /// Applies <paramref name="mutate"/> to the current value and publishes the result, under the
    /// same lock so two callers cannot interleave a read and a write.
    /// </summary>
    public T Mutate(Func<T, T> mutate)
    {
        lock (_gate)
        {
            _current = mutate(_current);
            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryWrite(_current);
            }

            return _current;
        }
    }

    public async IAsyncEnumerable<T> Watch([EnumeratorCancellation] CancellationToken ct = default)
    {
        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions { SingleReader = true });

        lock (_gate)
        {
            _subscribers.Add(channel);
            channel.Writer.TryWrite(_current);
        }

        try
        {
            await foreach (var value in channel.Reader.ReadAllAsync(ct).ConfigureAwait(false))
            {
                yield return value;
            }
        }
        finally
        {
            lock (_gate)
            {
                _subscribers.Remove(channel);
            }
        }
    }
}
