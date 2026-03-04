namespace MinCms.Core.Settings
{
    using System;

    /// <summary>
    /// Syslog server settings.
    /// </summary>
    public class SyslogServerSettings
    {
        #region Public-Members

        /// <summary>
        /// Hostname of the syslog server.
        /// </summary>
        public string Hostname
        {
            get => _Hostname;
            set { if (!String.IsNullOrEmpty(value)) _Hostname = value; }
        }

        /// <summary>
        /// Port of the syslog server.
        /// </summary>
        public int Port
        {
            get => _Port;
            set => _Port = (value >= 0 && value <= 65535) ? value : throw new ArgumentOutOfRangeException(nameof(Port));
        }

        #endregion

        #region Private-Members

        private string _Hostname = "localhost";
        private int _Port = 514;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SyslogServerSettings()
        {
        }

        #endregion
    }
}
