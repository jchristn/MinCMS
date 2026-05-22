namespace MinCms.Core.Settings
{
    using System;

    /// <summary>
    /// REST settings.
    /// </summary>
    public class RestSettings
    {
        #region Public-Members

        /// <summary>
        /// Hostname on which to listen.
        /// </summary>
        public string Hostname
        {
            get => _Hostname;
            set => _Hostname = (!String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Hostname)));
        }

        /// <summary>
        /// Port on which to listen.
        /// </summary>
        public int Port
        {
            get => _Port;
            set => _Port = (value >= 0 && value <= 65535 ? value : throw new ArgumentOutOfRangeException(nameof(Port)));
        }

        /// <summary>
        /// Enable or disable SSL.
        /// </summary>
        public bool Ssl { get; set; } = false;

        /// <summary>
        /// Socket read timeout in milliseconds for inbound uploads and downloads.
        /// </summary>
        public int ReadTimeoutMs
        {
            get => _ReadTimeoutMs;
            set => _ReadTimeoutMs = (value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(ReadTimeoutMs)));
        }

        /// <summary>
        /// Idle connection timeout in milliseconds.
        /// </summary>
        public int IdleTimeoutMs
        {
            get => _IdleTimeoutMs;
            set => _IdleTimeoutMs = (value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(IdleTimeoutMs)));
        }

        /// <summary>
        /// Stream buffer size in bytes used by the embedded web server.
        /// </summary>
        public int StreamBufferSize
        {
            get => _StreamBufferSize;
            set => _StreamBufferSize = (value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(StreamBufferSize)));
        }

        #endregion

        #region Private-Members

        private string _Hostname = "localhost";
        private int _Port = 8200;
        private int _ReadTimeoutMs = 120000;
        private int _IdleTimeoutMs = 600000;
        private int _StreamBufferSize = 65536;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RestSettings()
        {
        }

        #endregion
    }
}
