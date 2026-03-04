namespace MinCms.Core.Enums
{
    using System.Runtime.Serialization;

    /// <summary>
    /// API error codes.
    /// </summary>
    public enum ApiErrorEnum
    {
        /// <summary>
        /// Authentication failed.
        /// </summary>
        [EnumMember(Value = "AuthenticationFailed")]
        AuthenticationFailed,
        /// <summary>
        /// Bad request.
        /// </summary>
        [EnumMember(Value = "BadRequest")]
        BadRequest,
        /// <summary>
        /// Conflict.
        /// </summary>
        [EnumMember(Value = "Conflict")]
        Conflict,
        /// <summary>
        /// Internal error.
        /// </summary>
        [EnumMember(Value = "InternalError")]
        InternalError,
        /// <summary>
        /// Not found.
        /// </summary>
        [EnumMember(Value = "NotFound")]
        NotFound,
        /// <summary>
        /// Timeout.
        /// </summary>
        [EnumMember(Value = "Timeout")]
        Timeout,
        /// <summary>
        /// Request too large.
        /// </summary>
        [EnumMember(Value = "TooLarge")]
        TooLarge
    }
}
