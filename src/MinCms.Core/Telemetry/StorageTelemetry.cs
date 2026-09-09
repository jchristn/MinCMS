namespace MinCms.Core.Telemetry
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;
    using System.Threading.Tasks;

    /// <summary>
    /// Telemetry for the S3 object-storage layer. Emit rides the .NET base class library; a telemetry
    /// host subscribes to <see cref="MeterName"/> and <see cref="ActivitySourceName"/> to collect it.
    /// <para>
    /// Instruments: <c>mincms.storage.operations</c>, <c>mincms.storage.operation.errors</c>,
    /// <c>mincms.storage.operation.duration</c>, <c>mincms.storage.operations.inflight</c>,
    /// <c>mincms.storage.bytes.read</c>, and <c>mincms.storage.bytes.written</c>.
    /// </para>
    /// </summary>
    public static class StorageTelemetry
    {
        /// <summary>
        /// The meter name a telemetry host subscribes to for storage metrics.
        /// </summary>
        public const string MeterName = "MinCms.Storage";

        /// <summary>
        /// The activity-source name a telemetry host subscribes to for storage traces.
        /// </summary>
        public const string ActivitySourceName = "MinCms.Storage";

        /// <summary>
        /// The activity source used for storage-layer spans.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName);

        private static readonly Meter _Meter = new Meter(MeterName);
        private static readonly DomainInstruments _Instruments = new DomainInstruments(_Meter, ActivitySource, "mincms.storage", "storage");
        private static readonly Counter<long> _BytesRead = _Meter.CreateCounter<long>("mincms.storage.bytes.read", "By", "Bytes read from object storage.");
        private static readonly Counter<long> _BytesWritten = _Meter.CreateCounter<long>("mincms.storage.bytes.written", "By", "Bytes written to object storage.");

        /// <summary>
        /// Time and trace a storage operation that returns a value.
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="operation">The low-cardinality operation name (for example <c>put_object</c>).</param>
        /// <param name="body">The operation body.</param>
        /// <returns>The operation result.</returns>
        public static Task<T> MeasureAsync<T>(string operation, Func<Task<T>> body)
        {
            return _Instruments.MeasureAsync(operation, body);
        }

        /// <summary>
        /// Time and trace a storage operation that returns no value.
        /// </summary>
        /// <param name="operation">The low-cardinality operation name.</param>
        /// <param name="body">The operation body.</param>
        /// <returns>Task.</returns>
        public static Task MeasureAsync(string operation, Func<Task> body)
        {
            return _Instruments.MeasureAsync(operation, body);
        }

        /// <summary>
        /// Record bytes read from object storage.
        /// </summary>
        /// <param name="bytes">The number of bytes read.</param>
        public static void RecordBytesRead(long bytes)
        {
            if (bytes > 0) _BytesRead.Add(bytes);
        }

        /// <summary>
        /// Record bytes written to object storage.
        /// </summary>
        /// <param name="bytes">The number of bytes written.</param>
        public static void RecordBytesWritten(long bytes)
        {
            if (bytes > 0) _BytesWritten.Add(bytes);
        }
    }
}
