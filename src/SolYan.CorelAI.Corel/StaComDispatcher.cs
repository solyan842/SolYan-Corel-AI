using System.Collections.Concurrent;

namespace SolYan.CorelAI.Corel;

public sealed class StaComDispatcher : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;
    private bool _disposed;

    public StaComDispatcher()
    {
        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "SolYan Corel COM STA"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<T> InvokeAsync<T>(Func<T> action, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

        _queue.Add(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                tcs.TrySetCanceled(cancellationToken);
                return;
            }

            try
            {
                tcs.TrySetResult(action());
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
        }, cancellationToken);

        return tcs.Task;
    }

    public Task InvokeAsync(Action action, CancellationToken cancellationToken = default) =>
        InvokeAsync(() =>
        {
            action();
            return true;
        }, cancellationToken);

    private void Run()
    {
        foreach (var action in _queue.GetConsumingEnumerable())
            action();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(5));
        _queue.Dispose();
    }
}
