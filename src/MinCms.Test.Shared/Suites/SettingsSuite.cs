namespace MinCms.Test.Shared
{
    using System;
    using System.Collections.Generic;
    using MinCms.Core.Settings;
    using MinCms.Test.Shared.Support;
    using Touchstone.Core;

    public static partial class MinCmsSuites
    {
        /// <summary>Settings defaults and validation.</summary>
        public static TestSuiteDescriptor SettingsSuite()
        {
            const string s = "Settings";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                // ---- ServerSettings ----
                Case(s, "Server.Defaults", "ServerSettings exposes non-null sections and a default admin key", () =>
                {
                    ServerSettings settings = new ServerSettings();
                    Check.NotNull(settings.Rest);
                    Check.NotNull(settings.S3);
                    Check.NotNull(settings.Logging);
                    Check.NotNull(settings.Cors);
                    Check.Equal(1, settings.AccessKeys.Count);
                    Check.Equal("Admin", settings.AccessKeys[0].Name);
                    Check.Equal("mincmsadmin", settings.AccessKeys[0].Key);
                }),

                Case(s, "Server.Rest.Null", "ServerSettings.Rest rejects null", () =>
                    Check.Throws<ArgumentNullException>(() => new ServerSettings().Rest = null)),

                Case(s, "Server.S3.Null", "ServerSettings.S3 rejects null", () =>
                    Check.Throws<ArgumentNullException>(() => new ServerSettings().S3 = null)),

                Case(s, "Server.Logging.Null", "ServerSettings.Logging rejects null", () =>
                    Check.Throws<ArgumentNullException>(() => new ServerSettings().Logging = null)),

                Case(s, "Server.Cors.NullDefaults", "ServerSettings.Cors null is replaced with defaults", () =>
                {
                    ServerSettings settings = new ServerSettings();
                    settings.Cors = null;
                    Check.NotNull(settings.Cors);
                    Check.Equal("*", settings.Cors.AllowedOrigins[0]);
                }),

                Case(s, "Server.AccessKeys.NullEmpty", "ServerSettings.AccessKeys null becomes empty list", () =>
                {
                    ServerSettings settings = new ServerSettings();
                    settings.AccessKeys = null;
                    Check.NotNull(settings.AccessKeys);
                    Check.Equal(0, settings.AccessKeys.Count);
                }),

                // ---- RestSettings ----
                Case(s, "Rest.Defaults", "RestSettings default values", () =>
                {
                    RestSettings rest = new RestSettings();
                    Check.Equal("localhost", rest.Hostname);
                    Check.Equal(8200, rest.Port);
                    Check.False(rest.Ssl);
                    Check.Equal(120000, rest.ReadTimeoutMs);
                    Check.Equal(600000, rest.IdleTimeoutMs);
                    Check.Equal(65536, rest.StreamBufferSize);
                }),

                Case(s, "Rest.Hostname.Empty", "RestSettings.Hostname rejects empty", () =>
                    Check.Throws<ArgumentNullException>(() => new RestSettings().Hostname = "")),

                Case(s, "Rest.Port.Range", "RestSettings.Port accepts 0..65535 and rejects out-of-range", () =>
                {
                    RestSettings rest = new RestSettings();
                    rest.Port = 0;
                    Check.Equal(0, rest.Port);
                    rest.Port = 65535;
                    Check.Equal(65535, rest.Port);
                    Check.Throws<ArgumentOutOfRangeException>(() => new RestSettings().Port = -1);
                    Check.Throws<ArgumentOutOfRangeException>(() => new RestSettings().Port = 65536);
                }),

                Case(s, "Rest.Timeouts.Positive", "RestSettings timeout and buffer values must be positive", () =>
                {
                    Check.Throws<ArgumentOutOfRangeException>(() => new RestSettings().ReadTimeoutMs = 0);
                    Check.Throws<ArgumentOutOfRangeException>(() => new RestSettings().IdleTimeoutMs = 0);
                    Check.Throws<ArgumentOutOfRangeException>(() => new RestSettings().StreamBufferSize = 0);
                }),

                // ---- S3Settings ----
                Case(s, "S3.Defaults", "S3Settings default values", () =>
                {
                    S3Settings s3 = new S3Settings();
                    Check.True(s3.UseSsl);
                    Check.Equal(S3RequestStyle.VirtualHosted, s3.RequestStyle);
                    Check.Equal(16L * 1024L * 1024L, s3.MultipartThresholdBytes);
                    Check.Equal(8L * 1024L * 1024L, s3.MultipartPartSizeBytes);
                    Check.Equal("", s3.AccessKey);
                    Check.Equal("", s3.Bucket);
                }),

                Case(s, "S3.Credentials.IgnoreEmpty", "S3Settings credential setters ignore empty values", () =>
                {
                    S3Settings s3 = new S3Settings();
                    s3.AccessKey = "AKIA";
                    s3.AccessKey = "";
                    Check.Equal("AKIA", s3.AccessKey);
                    s3.Bucket = "bucket";
                    s3.Bucket = null;
                    Check.Equal("bucket", s3.Bucket);
                }),

                Case(s, "S3.MultipartThreshold.Positive", "S3Settings.MultipartThresholdBytes must be positive", () =>
                {
                    S3Settings s3 = new S3Settings();
                    s3.MultipartThresholdBytes = 1;
                    Check.Equal(1L, s3.MultipartThresholdBytes);
                    Check.Throws<ArgumentOutOfRangeException>(() => new S3Settings().MultipartThresholdBytes = 0);
                    Check.Throws<ArgumentOutOfRangeException>(() => new S3Settings().MultipartThresholdBytes = -5);
                }),

                Case(s, "S3.MultipartPartSize.Minimum", "S3Settings.MultipartPartSizeBytes enforces the 5 MiB minimum", () =>
                {
                    S3Settings s3 = new S3Settings();
                    s3.MultipartPartSizeBytes = 5L * 1024L * 1024L;
                    Check.Equal(5L * 1024L * 1024L, s3.MultipartPartSizeBytes);
                    Check.Throws<ArgumentOutOfRangeException>(() => new S3Settings().MultipartPartSizeBytes = (5L * 1024L * 1024L) - 1);
                }),

                Case(s, "S3.Endpoint.Nullable", "S3Settings.EndpointUrl accepts a value or null", () =>
                {
                    S3Settings s3 = new S3Settings();
                    s3.EndpointUrl = "https://minio.local:9000";
                    Check.Equal("https://minio.local:9000", s3.EndpointUrl);
                    s3.EndpointUrl = null;
                    Check.Null(s3.EndpointUrl);
                }),

                // ---- CorsSettings ----
                Case(s, "Cors.Defaults", "CorsSettings default values", () =>
                {
                    CorsSettings cors = new CorsSettings();
                    Check.Equal("*", cors.AllowedOrigins[0]);
                    Check.True(cors.AllowedMethods.Contains("OPTIONS"));
                    Check.True(cors.AllowedMethods.Contains("GET"));
                    Check.True(cors.AllowedMethods.Contains("POST"));
                    Check.Equal("*", cors.AllowedHeaders[0]);
                    Check.True(cors.ExposeHeaders.Contains("ETag"));
                    Check.Equal(86400, cors.MaxAgeSeconds);
                }),

                Case(s, "Cors.Normalize", "CorsSettings normalizes lists (trim, distinct, empty->default)", () =>
                {
                    CorsSettings cors = new CorsSettings();
                    cors.AllowedOrigins = new List<string> { " https://a ", "https://a", "  ", "https://b" };
                    Check.Equal(2, cors.AllowedOrigins.Count);
                    Check.True(cors.AllowedOrigins.Contains("https://a"));
                    Check.True(cors.AllowedOrigins.Contains("https://b"));

                    cors.AllowedOrigins = null;
                    Check.Equal("*", cors.AllowedOrigins[0]);

                    cors.AllowedMethods = new List<string>();
                    Check.True(cors.AllowedMethods.Count > 0);
                }),

                Case(s, "Cors.MaxAge.NonNegative", "CorsSettings.MaxAgeSeconds rejects negatives", () =>
                {
                    CorsSettings cors = new CorsSettings();
                    cors.MaxAgeSeconds = 0;
                    Check.Equal(0, cors.MaxAgeSeconds);
                    Check.Throws<ArgumentOutOfRangeException>(() => new CorsSettings().MaxAgeSeconds = -1);
                }),

                // ---- AccessKeyEntry ----
                Case(s, "AccessKey.Defaults", "AccessKeyEntry default values", () =>
                {
                    AccessKeyEntry entry = new AccessKeyEntry();
                    Check.Equal("Admin", entry.Name);
                    Check.Equal("mincmsadmin", entry.Key);
                }),

                Case(s, "AccessKey.Ctor", "AccessKeyEntry(name, key) assigns members", () =>
                {
                    AccessKeyEntry entry = new AccessKeyEntry("Ops", "secret");
                    Check.Equal("Ops", entry.Name);
                    Check.Equal("secret", entry.Key);
                }),

                Case(s, "AccessKey.Ctor.Null", "AccessKeyEntry(name, key) rejects null arguments", () =>
                {
                    Check.Throws<ArgumentNullException>(() => new AccessKeyEntry(null, "k"));
                    Check.Throws<ArgumentNullException>(() => new AccessKeyEntry("n", null));
                }),

                Case(s, "AccessKey.Empty", "AccessKeyEntry setters reject empty", () =>
                {
                    Check.Throws<ArgumentNullException>(() => new AccessKeyEntry().Name = "");
                    Check.Throws<ArgumentNullException>(() => new AccessKeyEntry().Key = "");
                }),

                // ---- LoggingSettings ----
                Case(s, "Logging.Defaults", "LoggingSettings default values", () =>
                {
                    LoggingSettings logging = new LoggingSettings();
                    Check.True(logging.ConsoleLogging);
                    Check.Equal(0, logging.MinimumSeverity);
                    Check.False(logging.EnableColors);
                    Check.True(logging.FileLogging);
                    Check.Equal("./logs/", logging.LogDirectory);
                    Check.Equal("mincms.log", logging.LogFilename);
                    Check.True(logging.IncludeDateInFilename);
                    Check.NotNull(logging.Servers);
                    Check.Equal(0, logging.Servers.Count);
                }),

                Case(s, "Logging.Severity.Range", "LoggingSettings.MinimumSeverity accepts 0..7", () =>
                {
                    LoggingSettings logging = new LoggingSettings();
                    logging.MinimumSeverity = 7;
                    Check.Equal(7, logging.MinimumSeverity);
                    logging.MinimumSeverity = 0;
                    Check.Equal(0, logging.MinimumSeverity);
                    Check.Throws<ArgumentOutOfRangeException>(() => new LoggingSettings().MinimumSeverity = -1);
                    Check.Throws<ArgumentOutOfRangeException>(() => new LoggingSettings().MinimumSeverity = 8);
                }),

                Case(s, "Logging.Directory.IgnoreEmpty", "LoggingSettings.LogDirectory ignores empty values", () =>
                {
                    LoggingSettings logging = new LoggingSettings();
                    logging.LogDirectory = "/var/log/mincms";
                    logging.LogDirectory = "";
                    Check.Equal("/var/log/mincms", logging.LogDirectory);
                }),

                // ---- SyslogServerSettings ----
                Case(s, "Syslog.Defaults", "SyslogServerSettings default values", () =>
                {
                    SyslogServerSettings syslog = new SyslogServerSettings();
                    Check.Equal("localhost", syslog.Hostname);
                    Check.Equal(514, syslog.Port);
                }),

                Case(s, "Syslog.Port.Range", "SyslogServerSettings.Port accepts 0..65535 and rejects out-of-range", () =>
                {
                    SyslogServerSettings syslog = new SyslogServerSettings();
                    syslog.Port = 0;
                    Check.Equal(0, syslog.Port);
                    syslog.Port = 65535;
                    Check.Equal(65535, syslog.Port);
                    Check.Throws<ArgumentOutOfRangeException>(() => new SyslogServerSettings().Port = -1);
                    Check.Throws<ArgumentOutOfRangeException>(() => new SyslogServerSettings().Port = 65536);
                })
            };

            return new TestSuiteDescriptor(s, "Settings defaults and validation", cases);
        }
    }
}
