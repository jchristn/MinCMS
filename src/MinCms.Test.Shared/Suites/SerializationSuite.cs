namespace MinCms.Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Specialized;
    using System.Net;
    using System.Text.Json;
    using MinCms.Core;
    using MinCms.Core.Enums;
    using MinCms.Core.Serialization;
    using MinCms.Core.Settings;
    using MinCms.Test.Shared.Support;
    using Touchstone.Core;

    public static partial class MinCmsSuites
    {
        /// <summary>Serializer and custom JSON converter behavior.</summary>
        public static TestSuiteDescriptor SerializationSuite()
        {
            const string s = "Serialization";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(s, "Serializer.NullObject", "SerializeJson(null) returns null", () =>
                {
                    Serializer serializer = new Serializer();
                    Check.Null(serializer.SerializeJson(null));
                }),

                Case(s, "Serializer.EmptyJson", "DeserializeJson rejects empty input", () =>
                {
                    Serializer serializer = new Serializer();
                    Check.Throws<ArgumentNullException>(() => serializer.DeserializeJson<Collection>(""));
                }),

                Case(s, "Serializer.RoundTrip", "Collection round-trips through JSON", () =>
                {
                    Serializer serializer = new Serializer();
                    Collection original = new Collection("Widgets", "widgets")
                    {
                        Id = "id-123",
                        IsActive = false,
                        CreatedUtc = new DateTime(2026, 3, 4, 5, 6, 7, DateTimeKind.Utc)
                    };

                    string json = serializer.SerializeJson(original, false);
                    Collection copy = serializer.DeserializeJson<Collection>(json);

                    Check.Equal(original.Id, copy.Id);
                    Check.Equal(original.Name, copy.Name);
                    Check.Equal(original.Slug, copy.Slug);
                    Check.Equal(original.IsActive, copy.IsActive);
                    Check.Equal(original.CreatedUtc, copy.CreatedUtc);
                }),

                Case(s, "Serializer.PrettyVsCompact", "Pretty output is indented; compact is single line", () =>
                {
                    Serializer serializer = new Serializer();
                    Collection value = new Collection("Name", "slug");
                    string pretty = serializer.SerializeJson(value, true);
                    string compact = serializer.SerializeJson(value, false);
                    Check.Contains("\n", pretty, StringComparison.Ordinal, "Pretty JSON should contain newlines");
                    Check.DoesNotContain("\n", compact, StringComparison.Ordinal, "Compact JSON should not contain newlines");
                }),

                Case(s, "Serializer.CopyObject", "CopyObject produces an independent deep copy", () =>
                {
                    Serializer serializer = new Serializer();
                    Collection original = new Collection("Name", "slug") { Id = "orig" };
                    Collection copy = serializer.CopyObject<Collection>(original);
                    copy.Name = "Changed";
                    Check.Equal("Name", original.Name);
                    Check.Equal("Changed", copy.Name);
                    Check.Equal("orig", copy.Id);
                }),

                Case(s, "Serializer.FromBytes", "DeserializeJson(byte[]) parses UTF-8 JSON and rejects empty", () =>
                {
                    Serializer serializer = new Serializer();
                    byte[] bytes = System.Text.Encoding.UTF8.GetBytes("{\"Name\":\"N\",\"Slug\":\"s\"}");
                    Collection value = serializer.DeserializeJson<Collection>(bytes);
                    Check.Equal("N", value.Name);
                    Check.Throws<ArgumentNullException>(() => serializer.DeserializeJson<Collection>(Array.Empty<byte>()));
                }),

                // ---- DateTimeConverter ----
                Case(s, "DateTime.WriteFormat", "DateTimeConverter writes the canonical microsecond-precision UTC format", () =>
                {
                    Serializer serializer = new Serializer();
                    Collection value = new Collection("N", "s")
                    {
                        CreatedUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)
                    };
                    string json = serializer.SerializeJson(value, false);
                    Check.Contains("2026-01-02T03:04:05.000000Z", json, StringComparison.Ordinal);
                }),

                Case(s, "DateTime.ParseDateOnly", "DateTimeConverter parses a date-only value", () =>
                {
                    Serializer serializer = new Serializer();
                    Collection value = serializer.DeserializeJson<Collection>("{\"Name\":\"N\",\"Slug\":\"s\",\"CreatedUtc\":\"2026-01-02\"}");
                    Check.Equal(2026, value.CreatedUtc.Year);
                    Check.Equal(1, value.CreatedUtc.Month);
                    Check.Equal(2, value.CreatedUtc.Day);
                }),

                Case(s, "DateTime.ParseInvalid", "DateTimeConverter rejects an unparseable value", () =>
                {
                    Serializer serializer = new Serializer();
                    Check.Throws<JsonException>(() =>
                        serializer.DeserializeJson<Collection>("{\"Name\":\"N\",\"Slug\":\"s\",\"CreatedUtc\":\"not-a-date\"}"));
                }),

                // ---- StrictEnumConverter ----
                Case(s, "Enum.WriteName", "Enums serialize by name", () =>
                {
                    Serializer serializer = new Serializer();
                    string json = serializer.SerializeJson(new ApiErrorResponse { Error = ApiErrorEnum.NotFound }, false);
                    Check.Contains("\"Error\":\"NotFound\"", json, StringComparison.Ordinal);
                }),

                Case(s, "Enum.ReadName", "Strict enum converter parses a valid name (case-insensitive)", () =>
                {
                    Serializer serializer = new Serializer();
                    ApiErrorResponse value = serializer.DeserializeJson<ApiErrorResponse>("{\"Error\":\"conflict\"}");
                    Check.Equal(ApiErrorEnum.Conflict, value.Error);
                }),

                Case(s, "Enum.ReadInvalidName", "Strict enum converter rejects an undefined name", () =>
                {
                    Serializer serializer = new Serializer();
                    Check.Throws<JsonException>(() => serializer.DeserializeJson<ApiErrorResponse>("{\"Error\":\"Nope\"}"));
                }),

                Case(s, "Enum.ReadDefinedNumber", "Strict enum converter accepts a defined numeric value", () =>
                {
                    Serializer serializer = new Serializer();
                    ApiErrorResponse value = serializer.DeserializeJson<ApiErrorResponse>("{\"Error\":4}");
                    Check.Equal(ApiErrorEnum.NotFound, value.Error);
                }),

                Case(s, "Enum.ReadUndefinedNumber", "Strict enum converter rejects an undefined numeric value", () =>
                {
                    Serializer serializer = new Serializer();
                    Check.Throws<JsonException>(() => serializer.DeserializeJson<ApiErrorResponse>("{\"Error\":99}"));
                }),

                // ---- IPAddressConverter ----
                Case(s, "IPAddress.RoundTrip", "IPAddressConverter round-trips an address", () =>
                {
                    JsonSerializerOptions options = new JsonSerializerOptions();
                    options.Converters.Add(new IPAddressConverter());
                    string json = JsonSerializer.Serialize(IPAddress.Parse("10.20.30.40"), options);
                    Check.Equal("\"10.20.30.40\"", json);
                    IPAddress parsed = JsonSerializer.Deserialize<IPAddress>(json, options);
                    Check.Equal("10.20.30.40", parsed.ToString());
                }),

                // ---- NameValueCollectionConverter ----
                Case(s, "NameValueCollection.RoundTrip", "NameValueCollectionConverter round-trips single and multi values", () =>
                {
                    JsonSerializerOptions options = new JsonSerializerOptions();
                    options.Converters.Add(new NameValueCollectionConverter());

                    NameValueCollection collection = new NameValueCollection();
                    collection.Add("single", "one");
                    collection.Add("multi", "a");
                    collection.Add("multi", "b");

                    string json = JsonSerializer.Serialize(collection, options);
                    NameValueCollection parsed = JsonSerializer.Deserialize<NameValueCollection>(json, options);

                    Check.Equal("one", parsed["single"]);
                    string[] multi = parsed.GetValues("multi");
                    Check.NotNull(multi);
                    Check.Equal(2, multi.Length);
                }),

                // ---- ExceptionConverter ----
                Case(s, "Exception.Write", "ExceptionConverter serializes exception details", () =>
                {
                    Serializer serializer = new Serializer();
                    string json = serializer.SerializeJson(new InvalidOperationException("boom"), false);
                    Check.Contains("boom", json, StringComparison.Ordinal);
                }),

                Case(s, "Exception.ReadDisallowed", "ExceptionConverter refuses to deserialize", () =>
                {
                    JsonSerializerOptions options = new JsonSerializerOptions();
                    options.Converters.Add(new ExceptionConverter<Exception>());
                    Check.Throws<NotSupportedException>(() => JsonSerializer.Deserialize<Exception>("{\"Message\":\"x\"}", options));
                }),

                // ---- Full settings round-trip ----
                Case(s, "ServerSettings.RoundTrip", "ServerSettings round-trips including the S3 request style enum", () =>
                {
                    Serializer serializer = new Serializer();
                    ServerSettings settings = new ServerSettings();
                    settings.Rest.Port = 9300;
                    settings.S3.Bucket = "content";
                    settings.S3.RequestStyle = S3RequestStyle.PathStyle;
                    settings.Cors.MaxAgeSeconds = 120;
                    settings.Logging.MinimumSeverity = 3;

                    string json = serializer.SerializeJson(settings, true);
                    ServerSettings copy = serializer.DeserializeJson<ServerSettings>(json);

                    Check.Equal(9300, copy.Rest.Port);
                    Check.Equal("content", copy.S3.Bucket);
                    Check.Equal(S3RequestStyle.PathStyle, copy.S3.RequestStyle);
                    Check.Equal(120, copy.Cors.MaxAgeSeconds);
                    Check.Equal(3, copy.Logging.MinimumSeverity);
                    Check.Equal(1, copy.AccessKeys.Count);
                }),

                Case(s, "ServerSettings.NullCorsDefaults", "ServerSettings with null Cors deserializes to defaults", () =>
                {
                    Serializer serializer = new Serializer();
                    ServerSettings settings = serializer.DeserializeJson<ServerSettings>("{\"Cors\":null}");
                    Check.NotNull(settings.Cors);
                    Check.Equal("*", settings.Cors.AllowedOrigins[0]);
                    Check.True(settings.Cors.AllowedMethods.Contains("OPTIONS"));
                    Check.Equal("*", settings.Cors.AllowedHeaders[0]);
                })
            };

            return new TestSuiteDescriptor(s, "Serialization and JSON converters", cases);
        }
    }
}
