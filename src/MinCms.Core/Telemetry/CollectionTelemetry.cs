namespace MinCms.Core.Telemetry
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading.Tasks;

    /// <summary>
    /// Telemetry for the collection-management application layer. Emit rides the .NET base class
    /// library; a telemetry host subscribes to <see cref="MeterName"/> and
    /// <see cref="ActivitySourceName"/> to collect it.
    /// <para>
    /// Instruments (dotted, UCUM-unit names following the OpenTelemetry semantic-convention style):
    /// <c>mincms.collection.operations</c>, <c>mincms.collection.operation.errors</c>,
    /// <c>mincms.collection.operation.duration</c>, and <c>mincms.collection.operations.inflight</c>,
    /// each tagged with <c>operation</c> (and <c>outcome</c> for count/duration).
    /// </para>
    /// </summary>
    public static class CollectionTelemetry
    {
        /// <summary>
        /// The meter name a telemetry host subscribes to for collection metrics.
        /// </summary>
        public const string MeterName = "MinCms.Collections";

        /// <summary>
        /// The activity-source name a telemetry host subscribes to for collection traces.
        /// </summary>
        public const string ActivitySourceName = "MinCms.Collections";

        /// <summary>
        /// The activity source used for collection-layer spans.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName);

        private static readonly Meter _Meter = new Meter(MeterName);
        private static readonly DomainInstruments _Instruments = new DomainInstruments(_Meter, ActivitySource, "mincms.collection", "collections");

        /// <summary>
        /// Time and trace a collection operation that returns a value.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="operation">The low-cardinality operation name (for example <c>create_collection</c>).</param>
        /// <param name="body">The operation body.</param>
        /// <returns>The operation result.</returns>
        public static Task<T> MeasureAsync<T>(string operation, Func<Task<T>> body)
        {
            return _Instruments.MeasureAsync(operation, body);
        }

        /// <summary>
        /// Time and trace a collection operation that returns no value.
        /// </summary>
        /// <param name="operation">The low-cardinality operation name.</param>
        /// <param name="body">The operation body.</param>
        /// <returns>Task.</returns>
        public static Task MeasureAsync(string operation, Func<Task> body)
        {
            return _Instruments.MeasureAsync(operation, body);
        }
    }
}
