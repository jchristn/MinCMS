namespace MinCms.Core
{
    using System;

    /// <summary>
    /// Constants.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Software version.
        /// </summary>
        public static string Version = "v1.0.0";

        /// <summary>
        /// Logo.
        /// </summary>
        public static string Logo =
            """

                          $$\                                                
                          \__|                                               
            $$$$$$\$$$$\  $$\ $$$$$$$\      $$$$$$$\ $$$$$$\$$$$\   $$$$$$$\ 
            $$  _$$  _$$\ $$ |$$  __$$\    $$  _____|$$  _$$  _$$\ $$  _____|
            $$ / $$ / $$ |$$ |$$ |  $$ |   $$ /      $$ / $$ / $$ |\$$$$$$\  
            $$ | $$ | $$ |$$ |$$ |  $$ |   $$ |      $$ | $$ | $$ | \____$$\ 
            $$ | $$ | $$ |$$ |$$ |  $$ |$$\\$$$$$$$\ $$ | $$ | $$ |$$$$$$$  |
            \__| \__| \__|\__|\__|  \__|\__|\_______|\__| \__| \__|\_______/ 
                                                         
            """;

        /// <summary>
        /// Product name.
        /// </summary>
        public static string ProductName = "MinCMS";

        /// <summary>
        /// Copyright.
        /// </summary>
        public static string Copyright = "(c)2026 Joel Christner";

        /// <summary>
        /// Timestamp format.
        /// </summary>
        public static string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.ffffffZ";

        /// <summary>
        /// Settings file.
        /// </summary>
        public static string SettingsFile = "./mincms.json";

        /// <summary>
        /// Log filename.
        /// </summary>
        public static string LogFilename = "mincms.log";

        /// <summary>
        /// Log directory.
        /// </summary>
        public static string LogDirectory = "./logs/";

        /// <summary>
        /// Authorization header.
        /// </summary>
        public static string AuthorizationHeader = "authorization";

        /// <summary>
        /// API key header.
        /// </summary>
        public static string ApiKeyHeader = "x-api-key";

        /// <summary>
        /// Forwarded for header.
        /// </summary>
        public static string ForwardedForHeader = "x-forwarded-for";

        /// <summary>
        /// Collections configuration key in S3.
        /// </summary>
        public static string CollectionsConfigKey = "config/collections.json";

        /// <summary>
        /// JSON content type.
        /// </summary>
        public static string JsonContentType = "application/json";

        /// <summary>
        /// HTML content type.
        /// </summary>
        public static string HtmlContentType = "text/html";

        /// <summary>
        /// XML content type.
        /// </summary>
        public static string XmlContentType = "application/xml";

        /// <summary>
        /// Binary content type.
        /// </summary>
        public static string BinaryContentType = "application/octet-stream";

        #region Environment-Variable-Names

        /// <summary>S3 access key environment variable.</summary>
        public static string S3AccessKeyEnvVar = "S3_ACCESS_KEY";

        /// <summary>S3 secret key environment variable.</summary>
        public static string S3SecretKeyEnvVar = "S3_SECRET_KEY";

        /// <summary>S3 bucket environment variable.</summary>
        public static string S3BucketEnvVar = "S3_BUCKET";

        /// <summary>S3 region environment variable.</summary>
        public static string S3RegionEnvVar = "S3_REGION";

        /// <summary>S3 endpoint URL environment variable.</summary>
        public static string S3EndpointEnvVar = "S3_ENDPOINT";

        /// <summary>S3 use SSL environment variable.</summary>
        public static string S3UseSslEnvVar = "S3_USE_SSL";

        /// <summary>S3 request style environment variable.</summary>
        public static string S3RequestStyleEnvVar = "S3_REQUEST_STYLE";

        /// <summary>Webserver hostname environment variable.</summary>
        public static string WebserverHostnameEnvVar = "WEBSERVER_HOSTNAME";

        /// <summary>Webserver port environment variable.</summary>
        public static string WebserverPortEnvVar = "WEBSERVER_PORT";

        #endregion

        /// <summary>
        /// Default HTML homepage.
        /// </summary>
        public static string HtmlHomepage =
            @"<html>" + Environment.NewLine +
            @"  <head>" + Environment.NewLine +
            @"    <title>MinCMS is Operational</title>" + Environment.NewLine +
            @"  </head>" + Environment.NewLine +
            @"  <body>" + Environment.NewLine +
            @"    <div>" + Environment.NewLine +
            @"      <pre>" + Environment.NewLine + Environment.NewLine +
            Logo + Environment.NewLine +
            @"      </pre>" + Environment.NewLine +
            @"    </div>" + Environment.NewLine +
            @"    <div style='font-family: Arial, sans-serif;'>" + Environment.NewLine +
            @"      <h2>MinCMS</h2>" + Environment.NewLine +
            @"      <p>Your node is operational.</p>" + Environment.NewLine +
            @"    <div>" + Environment.NewLine +
            @"  </body>" + Environment.NewLine +
            @"</html>" + Environment.NewLine;
    }
}
