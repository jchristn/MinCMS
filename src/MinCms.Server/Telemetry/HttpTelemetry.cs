namespace MinCms.Server.Telemetry
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.Metrics;

    /// <summary>
    /// Telemetry for the inbound HTTP (Watson webserver) layer. Emit rides the .NET base class
    /// library; a telemetry host subscribes to <see cref="MeterName"/> and
    /// <see cref="ActivitySourceName"/> to collect it.
    /// <para>
    /// Instrument names and tags follow the OpenTelemetry HTTP semantic conventions so they render in
    /// stock dashboards: <c>http.server.request.duration</c> (histogram, seconds),
    /// <c>http.server.active_requests</c> (up/down counter), and a MinCMS-specific
    /// <c>http.server.request.errors</c> counter. Tags: <c>http.request.method</c>,
    /// <c>http.route</c> (a low-cardinality route template, never a raw path),
    /// <c>http.response.status_code</c>, and <c>url.scheme</c>.
    /// </para>
    /// </summary>
    public static class HttpTelemetry
    {
        /// <summary>
        /// The meter name a telemetry host subscribes to for HTTP metrics.
        /// </summary>
        public const string MeterName = "MinCms.Http";

        /// <summary>
        /// The activity-source name a telemetry host subscribes to for HTTP traces.
        /// </summary>
        public const string ActivitySourceName = "MinCms.Http";

        /// <summary>
        /// The activity source used for inbound HTTP server spans.
        /// </summary>
        public static readonly ActivitySource ActivitySource = new ActivitySource(ActivitySourceName);

        private static readonly Meter _Meter = new Meter(MeterName);

        private static readonly Histogram<double> _RequestDuration =
            _Meter.CreateHistogram<double>("http.server.request.duration", "s", "Duration of inbound HTTP server requests.");

        private static readonly UpDownCounter<long> _ActiveRequests =
            _Meter.CreateUpDownCounter<long>("http.server.active_requests", "{request}", "Concurrent HTTP server requests in flight.");

        private static readonly Counter<long> _RequestErrors =
            _Meter.CreateCounter<long>("http.server.request.errors", "{request}", "Inbound HTTP server requests that returned a 5xx status.");

        private static readonly Counter<long> _AuthFailures =
            _Meter.CreateCounter<long>("mincms.http.auth.failures", "{request}", "Requests rejected during authentication.");

        /// <summary>
        /// Increment the in-flight request gauge.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="scheme">The URL scheme (http or https).</param>
        public static void IncrementActiveRequests(string method, string scheme)
        {
            _ActiveRequests.Add(1,
                new KeyValuePair<string, object?>("http.request.method", method),
                new KeyValuePair<string, object?>("url.scheme", scheme));
        }

        /// <summary>
        /// Decrement the in-flight request gauge.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="scheme">The URL scheme (http or https).</param>
        public static void DecrementActiveRequests(string method, string scheme)
        {
            _ActiveRequests.Add(-1,
                new KeyValuePair<string, object?>("http.request.method", method),
                new KeyValuePair<string, object?>("url.scheme", scheme));
        }

        /// <summary>
        /// Record a completed HTTP request: its duration and, when the status is 5xx, an error.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="route">The low-cardinality route template.</param>
        /// <param name="statusCode">The HTTP response status code.</param>
        /// <param name="scheme">The URL scheme (http or https).</param>
        /// <param name="seconds">The request duration in seconds.</param>
        public static void RecordRequest(string method, string route, int statusCode, string scheme, double seconds)
        {
            TagList tags = new TagList
            {
                { "http.request.method", method },
                { "http.route", route },
                { "http.response.status_code", statusCode },
                { "url.scheme", scheme }
            };

            _RequestDuration.Record(seconds, tags);

            if (statusCode >= 500)
            {
                _RequestErrors.Add(1,
                    new KeyValuePair<string, object?>("http.request.method", method),
                    new KeyValuePair<string, object?>("http.route", route),
                    new KeyValuePair<string, object?>("http.response.status_code", statusCode));
            }
        }

        /// <summary>
        /// Record a request rejected during authentication.
        /// </summary>
        /// <param name="route">The low-cardinality route template.</param>
        public static void RecordAuthFailure(string route)
        {
            _AuthFailures.Add(1, new KeyValuePair<string, object?>("http.route", route));
        }

        /// <summary>
        /// Start an inbound HTTP server span, or return null when nothing is sampling.
        /// </summary>
        /// <param name="method">The HTTP method.</param>
        /// <param name="route">The low-cardinality route template.</param>
        /// <param name="scheme">The URL scheme (http or https).</param>
        /// <returns>The started activity, or null.</returns>
        public static Activity? StartServerSpan(string method, string route, string scheme)
        {
            Activity? activity = ActivitySource.StartActivity(method + " " + route, ActivityKind.Server);
            if (activity != null)
            {
                activity.SetTag("http.request.method", method);
                activity.SetTag("http.route", route);
                activity.SetTag("url.scheme", scheme);
            }
            return activity;
        }

        /// <summary>
        /// Record an exception on a span as a standard <c>exception</c> event and mark it errored.
        /// </summary>
        /// <param name="activity">The span, or null.</param>
        /// <param name="e">The exception.</param>
        public static void RecordException(Activity? activity, Exception e)
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

        /// <summary>
        /// Reduce a raw request path to a low-cardinality route template. Unknown paths collapse to
        /// <c>other</c> so metric time-series stay bounded.
        /// </summary>
        /// <param name="rawUrl">The raw request URL (query string tolerated).</param>
        /// <returns>A route template such as <c>/v1.0/collections/{slug}</c>.</returns>
        public static string NormalizeRoute(string rawUrl)
        {
            if (String.IsNullOrEmpty(rawUrl)) return "other";

            string path = rawUrl;
            int queryIndex = path.IndexOf('?');
            if (queryIndex >= 0) path = path.Substring(0, queryIndex);
            if (path.Length > 1 && path.EndsWith("/", StringComparison.Ordinal)) path = path.TrimEnd('/');
            if (path.Length == 0) path = "/";

            if (path == "/") return "/";
            if (String.Equals(path, "/openapi.json", StringComparison.OrdinalIgnoreCase)) return "/openapi.json";
            if (String.Equals(path, "/swagger", StringComparison.OrdinalIgnoreCase)) return "/swagger";

            string[] segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Length >= 1 && String.Equals(segments[0], "v1.0", StringComparison.OrdinalIgnoreCase))
            {
                if (segments.Length >= 2 && String.Equals(segments[1], "collections", StringComparison.OrdinalIgnoreCase))
                {
                    if (segments.Length == 2) return "/v1.0/collections";
                    if (segments.Length == 3) return "/v1.0/collections/{slug}";
                    if (segments.Length == 4 && String.Equals(segments[3], "files", StringComparison.OrdinalIgnoreCase)) return "/v1.0/collections/{slug}/files";
                    if (segments.Length == 5 && String.Equals(segments[3], "files", StringComparison.OrdinalIgnoreCase)) return "/v1.0/collections/{slug}/files/{fileName}";
                }
            }

            if (segments.Length >= 1 && String.Equals(segments[0], "download", StringComparison.OrdinalIgnoreCase))
            {
                if (segments.Length == 2) return "/download/{slug}";
                if (segments.Length == 3 && String.Equals(segments[2], "sitemap.xml", StringComparison.OrdinalIgnoreCase)) return "/download/{slug}/sitemap.xml";
                if (segments.Length == 3) return "/download/{slug}/{fileName}";
            }

            return "other";
        }
    }
}
