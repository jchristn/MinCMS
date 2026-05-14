namespace MinCms.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Cross-origin resource sharing settings.
    /// </summary>
    public class CorsSettings
    {
        #region Public-Members

        /// <summary>
        /// Allowed origins.
        /// Use "*" to allow any origin.
        /// </summary>
        public List<string> AllowedOrigins
        {
            get => _AllowedOrigins;
            set => _AllowedOrigins = NormalizeList(value, _DefaultAllowedOrigins);
        }

        /// <summary>
        /// Allowed HTTP methods for preflight responses.
        /// </summary>
        public List<string> AllowedMethods
        {
            get => _AllowedMethods;
            set => _AllowedMethods = NormalizeList(value, _DefaultAllowedMethods);
        }

        /// <summary>
        /// Allowed request headers.
        /// Use "*" to mirror the requested headers.
        /// </summary>
        public List<string> AllowedHeaders
        {
            get => _AllowedHeaders;
            set => _AllowedHeaders = NormalizeList(value, _DefaultAllowedHeaders);
        }

        /// <summary>
        /// Response headers that browsers may expose to calling JavaScript.
        /// </summary>
        public List<string> ExposeHeaders
        {
            get => _ExposeHeaders;
            set => _ExposeHeaders = NormalizeList(value, _DefaultExposeHeaders);
        }

        /// <summary>
        /// Browser preflight cache duration in seconds.
        /// </summary>
        public int MaxAgeSeconds
        {
            get => _MaxAgeSeconds;
            set => _MaxAgeSeconds = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(MaxAgeSeconds));
        }

        #endregion

        #region Private-Members

        private static readonly List<string> _DefaultAllowedOrigins = new List<string> { "*" };
        private static readonly List<string> _DefaultAllowedMethods = new List<string> { "GET", "HEAD", "OPTIONS", "POST", "PUT", "PATCH", "DELETE" };
        private static readonly List<string> _DefaultAllowedHeaders = new List<string> { "*" };
        private static readonly List<string> _DefaultExposeHeaders = new List<string> { "Content-Disposition", "Content-Length", "Content-Type", "ETag" };

        private List<string> _AllowedOrigins = new List<string>(_DefaultAllowedOrigins);
        private List<string> _AllowedMethods = new List<string>(_DefaultAllowedMethods);
        private List<string> _AllowedHeaders = new List<string>(_DefaultAllowedHeaders);
        private List<string> _ExposeHeaders = new List<string>(_DefaultExposeHeaders);
        private int _MaxAgeSeconds = 86400;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CorsSettings()
        {
        }

        #endregion

        #region Private-Methods

        private static List<string> NormalizeList(List<string> values, List<string> defaults)
        {
            if (values == null || values.Count < 1)
                return new List<string>(defaults);

            List<string> normalized =
                values
                .Where(v => !String.IsNullOrWhiteSpace(v))
                .Select(v => v.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (normalized.Count < 1)
                return new List<string>(defaults);

            return normalized;
        }

        #endregion
    }
}
