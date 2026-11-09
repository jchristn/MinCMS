namespace MinCms.Server
{
    using System;
    using System.Reflection;
    using WatsonWebserver.Core;

    /// <summary>
    /// Tells Watson that a request body streamed through <see cref="HttpRequestBase.Data"/> has been fully read.
    /// Watson only cancels the request token on a client disconnect once the body is marked complete, and reading
    /// the Data stream does not mark it. Without this, an upload cancelled while the server is storing the file
    /// would still finish. The method is internal to Watson, so UploadCancellationSuite guards this behavior;
    /// remove this class once Watson marks the body complete when its content-length stream is exhausted.
    /// </summary>
    internal static class RequestBodyCompletion
    {
        private const string _MethodName = "MarkBodyComplete";

        /// <summary>
        /// Mark the request body as fully read. Request types without this concept are left alone.
        /// </summary>
        /// <param name="request">Watson request.</param>
        public static void Mark(HttpRequestBase request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));

            MethodInfo method = request.GetType().GetMethod(_MethodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            method?.Invoke(request, null);
        }
    }
}
