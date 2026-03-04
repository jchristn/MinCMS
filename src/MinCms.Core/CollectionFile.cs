namespace MinCms.Core
{
    using System;

    /// <summary>
    /// Represents a file stored within a collection's S3 prefix.
    /// </summary>
    public class CollectionFile
    {
        #region Public-Members

        /// <summary>
        /// Full S3 object key.
        /// </summary>
        public string Key
        {
            get => _Key;
            set => _Key = !String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Key));
        }

        /// <summary>
        /// File name (without prefix).
        /// </summary>
        public string FileName
        {
            get => _FileName;
            set => _FileName = !String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(FileName));
        }

        /// <summary>
        /// File size in bytes.
        /// </summary>
        public long Size { get; set; } = 0;

        /// <summary>
        /// UTC timestamp of last modification.
        /// </summary>
        public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Content type of the file.
        /// </summary>
        public string ContentType
        {
            get => _ContentType;
            set => _ContentType = !String.IsNullOrEmpty(value) ? value : Constants.BinaryContentType;
        }

        /// <summary>
        /// S3 ETag for the object.
        /// </summary>
        public string ETag { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Key = "";
        private string _FileName = "";
        private string _ContentType = Constants.BinaryContentType;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public CollectionFile()
        {
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
