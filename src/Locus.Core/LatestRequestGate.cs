using System;
using System.Threading;
using System.Threading.Tasks;

namespace Locus.Core
{
    /// <summary>One gate per editor/session. Publishing a result is separate from permission to modify any document.</summary>
    public sealed class LatestRequestGate<T> : IDisposable
    {
        private readonly object sync = new object();
        private long generation;
        private bool disposed;
        private CancellationTokenSource? pending;

        public async Task<RequestResult<T>> RunAsync(Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken = default)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            var current = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = current.Token;
            long request;
            CancellationTokenSource? previous;
            lock (sync)
            {
                if (disposed) { current.Dispose(); throw new ObjectDisposedException(nameof(LatestRequestGate<T>)); }
                request = ++generation; previous = pending; pending = current;
            }
            try
            {
                Cancel(previous);
                token.ThrowIfCancellationRequested();
                var value = await work(token).ConfigureAwait(false);
                lock (sync)
                {
                    if (disposed || generation != request || token.IsCancellationRequested) return RequestResult<T>.Discarded(request);
                    return RequestResult<T>.Published(request, value);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            { return RequestResult<T>.Discarded(request); }
            finally
            {
                lock (sync) { if (ReferenceEquals(pending, current)) pending = null; }
                current.Dispose();
            }
        }

        public void Invalidate()
        {
            CancellationTokenSource? old;
            lock (sync) { if (disposed) return; generation++; old = pending; pending = null; }
            Cancel(old);
        }
        public void Dispose()
        {
            CancellationTokenSource? old;
            lock (sync) { if (disposed) return; disposed = true; generation++; old = pending; pending = null; }
            Cancel(old);
        }
        private static void Cancel(CancellationTokenSource? source)
        {
            try { source?.Cancel(); } catch (ObjectDisposedException) { /* The request already completed. */ }
            catch (AggregateException) { /* A superseded consumer callback cannot prevent the new request from running. */ }
        }
    }

    public sealed class RequestResult<T>
    {
        public long Generation { get; }
        public bool IsCurrent { get; }
        public T? Value { get; }
        private RequestResult(long generation, bool isCurrent, T? value) { Generation = generation; IsCurrent = isCurrent; Value = value; }
        internal static RequestResult<T> Discarded(long generation) => new RequestResult<T>(generation, false, default);
        internal static RequestResult<T> Published(long generation, T value) => new RequestResult<T>(generation, true, value);
    }
}
