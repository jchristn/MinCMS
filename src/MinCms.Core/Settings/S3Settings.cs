namespace MinCms.Core.Settings
{
    using System;

    /// <summary>
    /// S3 connection settings.
    /// </summary>
    public class S3Settings
    {
        #region Public-Members

        /// <summary>
        /// AWS access key.
        /// </summary>
        public string AccessKey
        {
            get => _AccessKey;
            set { if (!String.IsNullOrEmpty(value)) _AccessKey = value; }
        }

        /// <summary>
        /// AWS secret key.
        /// </summary>
        public string SecretKey
        {
            get => _SecretKey;
            set { if (!String.IsNullOrEmpty(value)) _SecretKey = value; }
        }

        /// <summary>
        /// S3 bucket name.
        /// </summary>
        public string Bucket
        {
            get => _Bucket;
            set { if (!String.IsNullOrEmpty(value)) _Bucket = value; }
        }

        /// <summary>
        /// AWS region.
        /// </summary>
        public string Region
        {
            get => _Region;
            set { if (!String.IsNullOrEmpty(value)) _Region = value; }
        }

        /// <summary>
        /// Custom S3-compatible endpoint URL (e.g. for MinIO, Wasabi, etc.).
        /// When set, this is used instead of the default AWS region endpoint.
        /// </summary>
        public string EndpointUrl
        {
            get => _EndpointUrl;
            set => _EndpointUrl = value;
        }

        /// <summary>
        /// Whether to use SSL when connecting to the S3 endpoint.
        /// Default is true.
        /// </summary>
        public bool UseSsl { get; set; } = true;

        /// <summary>
        /// S3 request style (VirtualHosted or PathStyle).
        /// Default is VirtualHosted.
        /// </summary>
        public S3RequestStyle RequestStyle { get; set; } = S3RequestStyle.VirtualHosted;

        #endregion

        #region Private-Members

        private string _AccessKey = "";
        private string _SecretKey = "";
        private string _Bucket = "";
        private string _Region = "";
        private string _EndpointUrl = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public S3Settings()
        {
        }

        #endregion
    }
}
