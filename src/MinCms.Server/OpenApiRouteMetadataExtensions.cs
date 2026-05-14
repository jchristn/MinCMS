namespace MinCms.Server
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using WatsonWebserver.Core.OpenApi;

    /// <summary>
    /// Convenience extensions for Watson OpenAPI route metadata.
    /// </summary>
    public static class OpenApiRouteMetadataExtensions
    {
        /// <summary>
        /// Set the operation identifier.
        /// </summary>
        /// <param name="metadata">Metadata.</param>
        /// <param name="operationId">Operation identifier.</param>
        /// <returns>Metadata.</returns>
        public static OpenApiRouteMetadata WithOperationId(this OpenApiRouteMetadata metadata, string operationId)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            metadata.OperationId = operationId;
            return metadata;
        }

        /// <summary>
        /// Set the operation summary.
        /// </summary>
        /// <param name="metadata">Metadata.</param>
        /// <param name="summary">Summary.</param>
        /// <returns>Metadata.</returns>
        public static OpenApiRouteMetadata WithSummary(this OpenApiRouteMetadata metadata, string summary)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));
            metadata.Summary = summary;
            return metadata;
        }

        /// <summary>
        /// Require one or more security schemes for the operation.
        /// Each scheme is emitted as an alternative requirement.
        /// </summary>
        /// <param name="metadata">Metadata.</param>
        /// <param name="schemeNames">Security scheme names.</param>
        /// <returns>Metadata.</returns>
        public static OpenApiRouteMetadata RequireSecurity(this OpenApiRouteMetadata metadata, params string[] schemeNames)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));

            if (schemeNames == null || schemeNames.Length < 1)
            {
                metadata.Security = null;
                return metadata;
            }

            metadata.Security =
                schemeNames
                .Where(s => !String.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return metadata;
        }
    }
}
