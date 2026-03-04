namespace MinCms.Core.Settings
{
    /// <summary>
    /// S3 request style.
    /// </summary>
    public enum S3RequestStyle
    {
        /// <summary>
        /// Virtual hosted-style requests (default).
        /// </summary>
        VirtualHosted,

        /// <summary>
        /// Path-style requests.
        /// </summary>
        PathStyle
    }
}
