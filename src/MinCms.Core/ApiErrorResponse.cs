namespace MinCms.Core
{
    using System;
    using MinCms.Core.Enums;

    /// <summary>
    /// API error response.
    /// </summary>
    public class ApiErrorResponse
    {
        #region Public-Members

        /// <summary>
        /// Error.
        /// </summary>
        public ApiErrorEnum Error { get; set; } = ApiErrorEnum.AuthenticationFailed;

        /// <summary>
        /// Human-readable message.
        /// </summary>
        public string Message
        {
            get
            {
                switch (Error)
                {
                    case ApiErrorEnum.AuthenticationFailed:
                        return "Your authentication material was not accepted.";
                    case ApiErrorEnum.BadRequest:
                        return "We were unable to discern your request.  Please check your URL, query, and request body.";
                    case ApiErrorEnum.Conflict:
                        return "Operation failed as it would create a conflict with an existing resource.";
                    case ApiErrorEnum.InternalError:
                        return "An internal error has been encountered.";
                    case ApiErrorEnum.NotFound:
                        return "The requested resource was not found.";
                    case ApiErrorEnum.Timeout:
                        return "The request was not completed within the specified timeout interval.";
                    case ApiErrorEnum.TooLarge:
                        return "The size of your request exceeds the maximum allowed by this server.";
                    default:
                        return "An unknown error code '" + Error.ToString() + "' was encountered.";
                }
            }
        }

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode
        {
            get
            {
                switch (Error)
                {
                    case ApiErrorEnum.AuthenticationFailed:
                        return 401;
                    case ApiErrorEnum.BadRequest:
                        return 400;
                    case ApiErrorEnum.Conflict:
                        return 409;
                    case ApiErrorEnum.InternalError:
                        return 500;
                    case ApiErrorEnum.NotFound:
                        return 404;
                    case ApiErrorEnum.Timeout:
                        return 408;
                    case ApiErrorEnum.TooLarge:
                        return 413;
                    default:
                        return 500;
                }
            }
        }

        /// <summary>
        /// Additional contextual information.
        /// </summary>
        public object Context { get; set; } = null;

        /// <summary>
        /// Description.
        /// </summary>
        public string Description { get; set; } = null;

        #endregion

        #region Private-Members

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// API error response.
        /// </summary>
        public ApiErrorResponse()
        {
        }

        /// <summary>
        /// API error response.
        /// </summary>
        /// <param name="error">Error code.</param>
        /// <param name="context">Context.</param>
        /// <param name="description">Description.</param>
        public ApiErrorResponse(ApiErrorEnum error, object context = null, string description = null)
        {
            Error = error;
            Context = context;
            Description = description;
        }

        #endregion

        #region Public-Methods

        #endregion

        #region Private-Methods

        #endregion
    }
}
