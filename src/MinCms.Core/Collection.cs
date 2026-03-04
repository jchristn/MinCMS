namespace MinCms.Core
{
    using System;

    /// <summary>
    /// Collection definition representing a company whose products are sold.
    /// </summary>
    public class Collection
    {
        #region Public-Members

        /// <summary>
        /// Unique identifier.
        /// </summary>
        public string Id
        {
            get => _Id;
            set => _Id = !String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Id));
        }

        /// <summary>
        /// Collection name.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = !String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Name));
        }

        /// <summary>
        /// URL-friendly slug used as the S3 prefix and public download path segment.
        /// </summary>
        public string Slug
        {
            get => _Slug;
            set => _Slug = !String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Slug));
        }

        /// <summary>
        /// UTC timestamp of creation.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the collection is active.
        /// </summary>
        public bool IsActive { get; set; } = true;

        #endregion

        #region Private-Members

        private string _Id = Guid.NewGuid().ToString();
        private string _Name = "My Collection";
        private string _Slug = "my-collection";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public Collection()
        {
        }

        /// <summary>
        /// Instantiate with name and slug.
        /// </summary>
        /// <param name="name">Collection name.</param>
        /// <param name="slug">URL-friendly slug.</param>
        public Collection(string name, string slug)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (String.IsNullOrEmpty(slug)) throw new ArgumentNullException(nameof(slug));

            _Name = name;
            _Slug = slug;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
