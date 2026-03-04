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

        #endregion

        #region Private-Members

        private string _Hostname = "localhost";
        private int _Port = 8200;

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
