namespace MinCms.Core.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Server settings loaded from mincms.json.
    /// </summary>
    public class ServerSettings
    {
        #region Public-Members

        /// <summary>
        /// REST webserver settings.
        /// </summary>
        public RestSettings Rest
        {
            get => _Rest;
            set => _Rest = value ?? throw new ArgumentNullException(nameof(Rest));
        }

        /// <summary>
        /// S3 connection settings.
        /// </summary>
        public S3Settings S3
        {
            get => _S3;
            set => _S3 = value ?? throw new ArgumentNullException(nameof(S3));
        }

        /// <summary>
        /// List of access keys for dashboard authentication.
        /// </summary>
        public List<AccessKeyEntry> AccessKeys
        {
            get => _AccessKeys;
            set => _AccessKeys = value ?? new List<AccessKeyEntry>();
        }

        /// <summary>
        /// Logging settings.
        /// </summary>
        public LoggingSettings Logging
        {
            get => _Logging;
            set => _Logging = value ?? throw new ArgumentNullException(nameof(Logging));
        }

        #endregion

        #region Private-Members

        private RestSettings _Rest = new RestSettings();
        private S3Settings _S3 = new S3Settings();

        private List<AccessKeyEntry> _AccessKeys = new List<AccessKeyEntry>
        {
            new AccessKeyEntry("Admin", "mincmsadmin")
        };

        private LoggingSettings _Logging = new LoggingSettings();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public ServerSettings()
        {
        }

        #endregion
    }
}
