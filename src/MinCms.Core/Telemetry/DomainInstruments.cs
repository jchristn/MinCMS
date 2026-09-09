namespace MinCms.Core.Telemetry
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading.Tasks;

    /// <summary>
    /// A reusable bundle of the four instruments every MinCMS application-layer domain records
    /// (operation count, error count, duration histogram, in-flight up/down counter) plus an
    /// <see cref="ActivitySource"/> span, wrapped behind a single <see cref="MeasureAsync{T}"/> call.
    /// <para>
    /// Instruments emit through the .NET base class library, so they are a no-op until a telemetry
    /// host (Radiant) subscribes to the owning meter and activity-source names. Nothing here requires
    /// a host to be running.
    /// </para>
    /// </summary>
    internal sealed class DomainInstruments
    {
        private readonly ActivitySource _Source;
        private readonly string _Domain;
        private readonly Counter<long> _Operations;
        private readonly Counter<long> _Errors;
        private readonly Histogram<double> _Duration;
        private readonly UpDownCounter<long> _InFlight;

        /// <summary>
        /// Instantiate the instrument bundle for a domain.
        /// </summary>
        /// <param name="meter">The owning meter (named after the domain).</param>
        /// <param name="source">The owning activity source (named after the domain).</param>
        /// <param name="prefix">The dotted instrument-name prefix, for example <c>mincms.collection</c>.</param>
        /// <param name="domain">The short domain label stamped on every span, for example <c>collections</c>.</param>
        public DomainInstruments(Meter meter, ActivitySource source, string prefix, string domain)
        {
            _Source = source;
            _Domain = domain;
            _Operations = meter.CreateCounter<long>(prefix + ".operations", "{operation}", "Application operations invoked.");
            _Errors = meter.CreateCounter<long>(prefix + ".operation.errors", "{operation}", "Application operations that threw an exception.");
            _Duration = meter.CreateHistogram<double>(prefix + ".operation.duration", "s", "Duration of application operations.");
            _InFlight = meter.CreateUpDownCounter<long>(prefix + ".operations.inflight", "{operation}", "Application operations currently in flight.");
        }

        /// <summary>
        /// Time and trace an operation: start a span, count it, record its duration and outcome, and
        /// surface any exception on the span and the error counter. The instruments stay a no-op when
        /// nothing is subscribed.
        /// </summary>
        /// <typeparam name="T">The operation result type.</typeparam>
        /// <param name="operation">The low-cardinality operation name (for example <c>create_collection</c>).</param>
        /// <param name="body">The operation body to execute.</param>
        /// <returns>The operation result.</returns>
        public async Task<T> MeasureAsync<T>(string operation, Func<Task<T>> body)
        {
            KeyValuePair<string, object?> operationTag = new KeyValuePair<string, object?>("operation", operation);
            long startTimestamp = Stopwatch.GetTimestamp();

            _InFlight.Add(1, operationTag);

            using Activity activity = _Source.StartActivity(_Domain + "." + operation, ActivityKind.Internal);
            activity?.SetTag("mincms.domain", _Domain);
            activity?.SetTag("operation", operation);

            string outcome = "ok";

            try
            {
                T result = await body().ConfigureAwait(false);
                activity?.SetStatus(ActivityStatusCode.Ok);
                return result;
            }
            catch (Exception e)
            {
                outcome = "error";
                _Errors.Add(1, operationTag);
                RecordException(activity, e);
                throw;
            }
            finally
            {
                double seconds = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
                KeyValuePair<string, object?> outcomeTag = new KeyValuePair<string, object?>("outcome", outcome);
                _Operations.Add(1, operationTag, outcomeTag);
                _Duration.Record(seconds, operationTag, outcomeTag);
                _InFlight.Add(-1, operationTag);
            }
        }

        /// <summary>
        /// Time and trace an operation that returns no value.
        /// </summary>
        /// <param name="operation">The low-cardinality operation name.</param>
        /// <param name="body">The operation body to execute.</param>
        /// <returns>Task.</returns>
        public async Task MeasureAsync(string operation, Func<Task> body)
        {
            await MeasureAsync<object>(operation, async () =>
            {
                await body().ConfigureAwait(false);
                return null;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Record an exception on a span as a standard <c>exception</c> event and mark the span as
        /// errored. A no-op when the span is null (nothing sampling).
        /// </summary>
        /// <param name="activity">The span, or null.</param>
        /// <param name="e">The exception.</param>
        internal static void RecordException(Activity activity, Exception e)
        {
            if (activity == null || e == null) return;

            ActivityTagsCollection tags = new ActivityTagsCollection
            {
                { "exception.type", e.GetType().FullName },
                { "exception.message", e.Message }
            };
            if (e.StackTrace != null) tags["exception.stacktrace"] = e.StackTrace;

            activity.AddEvent(new ActivityEvent("exception", default, tags));
            activity.SetStatus(ActivityStatusCode.Error, e.Message);
        }
    }
}
