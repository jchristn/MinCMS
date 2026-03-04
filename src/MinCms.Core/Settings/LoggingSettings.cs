namespace MinCms.Core.Settings
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Logging settings.
    /// </summary>
    public class LoggingSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable or disable console logging.
        /// </summary>
        public bool ConsoleLogging { get; set; } = true;

        /// <summary>
        /// Minimum severity level for logging (0 through 7).
        /// </summary>
        public int MinimumSeverity
        {
            get => _MinimumSeverity;
            set => _MinimumSeverity = (value >= 0 && value <= 7) ? value : throw new ArgumentOutOfRangeException(nameof(MinimumSeverity));
        }

        /// <summary>
        /// Enable or disable color output in console logging.
        /// </summary>
        public bool EnableColors { get; set; } = false;

        /// <summary>
        /// Enable or disable file logging.
        /// </summary>
        public bool FileLogging { get; set; } = true;

        /// <summary>
        /// Log directory.
        /// </summary>
        public string LogDirectory
        {
            get => _LogDirectory;
            set { if (!String.IsNullOrEmpty(value)) _LogDirectory = value; }
        }

        /// <summary>
        /// Log filename.
        /// </summary>
        public string LogFilename
        {
            get => _LogFilename;
            set { if (!String.IsNullOrEmpty(value)) _LogFilename = value; }
        }

        /// <summary>
        /// Include date in log filename.
        /// </summary>
        public bool IncludeDateInFilename { get; set; } = true;

        /// <summary>
        /// List of syslog servers to which log messages should be sent.
        /// </summary>
        public List<SyslogServerSettings> Servers
        {
            get => _Servers;
            set { if (value != null) _Servers = value; }
        }

        #endregion

        #region Private-Members

        private int _MinimumSeverity = 0;
        private string _LogDirectory = "./logs/";
        private string _LogFilename = "mincms.log";
        private List<SyslogServerSettings> _Servers = new List<SyslogServerSettings>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public LoggingSettings()
        {
        }

        #endregion
    }
}
