namespace MinCms.Core.Settings
{
    using System;

    /// <summary>
    /// Access key entry for dashboard authentication.
    /// </summary>
    public class AccessKeyEntry
    {
        #region Public-Members

        /// <summary>
        /// Friendly name for the key holder.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => _Name = !String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Name));
        }

        /// <summary>
        /// Access key value.
        /// </summary>
        public string Key
        {
            get => _Key;
            set => _Key = !String.IsNullOrEmpty(value) ? value : throw new ArgumentNullException(nameof(Key));
        }

        #endregion

        #region Private-Members

        private string _Name = "Admin";
        private string _Key = "mincmsadmin";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AccessKeyEntry()
        {
        }

        /// <summary>
        /// Instantiate with name and key.
        /// </summary>
        /// <param name="name">Friendly name.</param>
        /// <param name="key">Access key value.</param>
        public AccessKeyEntry(string name, string key)
        {
            if (String.IsNullOrEmpty(name)) throw new ArgumentNullException(nameof(name));
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));

            _Name = name;
            _Key = key;
        }

        #endregion
    }
}
