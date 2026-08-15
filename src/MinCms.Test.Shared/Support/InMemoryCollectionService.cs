namespace MinCms.Test.Shared.Support
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MinCms.Core;
    using MinCms.Core.Services;

    /// <summary>
    /// In-memory <see cref="ICollectionService"/> used by API host integration tests.
    /// Seeded with a single active collection "alpha" containing "sample.txt".
    /// </summary>
    public sealed class InMemoryCollectionService : ICollectionService
    {
        private readonly object _Sync = new object();
        private readonly Dictionary<string, Collection> _Collections = new Dictionary<string, Collection>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, FileRecord>> _Files = new Dictionary<string, Dictionary<string, FileRecord>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Instantiate and seed the "alpha" collection.</summary>
        public InMemoryCollectionService()
        {
            Collection collection = new Collection("Alpha", "alpha")
            {
                Id = "alpha-id",
                CreatedUtc = DateTime.UtcNow.AddDays(-1),
                IsActive = true
            };

            _Collections[collection.Slug] = CloneCollection(collection);
            _Files[collection.Slug] = new Dictionary<string, FileRecord>(StringComparer.OrdinalIgnoreCase)
            {
                ["sample.txt"] = new FileRecord
                {
                    Content = Encoding.UTF8.GetBytes("seed file"),
                    ContentType = "text/plain",
                    LastModifiedUtc = DateTime.UtcNow.AddHours(-1),
                    ETag = "seed-etag"
                }
            };
        }

        /// <inheritdoc />
        public Task<List<Collection>> GetAllCollectionsAsync(CancellationToken token = default)
        {
            lock (_Sync)
            {
                return Task.FromResult(_Collections.Values.Select(CloneCollection).ToList());
            }
        }

        /// <inheritdoc />
        public Task<Collection> GetCollectionBySlugAsync(string slug, CancellationToken token = default)
        {
            lock (_Sync)
            {
                if (!_Collections.TryGetValue(slug, out Collection collection))
                    throw new KeyNotFoundException("Collection with slug '" + slug + "' not found.");

                return Task.FromResult(CloneCollection(collection));
            }
        }

        /// <inheritdoc />
        public Task<Collection> CreateCollectionAsync(string name, string slug, CancellationToken token = default)
        {
            lock (_Sync)
            {
                if (_Collections.ContainsKey(slug))
                    throw new InvalidOperationException("A collection with slug '" + slug + "' already exists.");

                Collection collection = new Collection(name, slug)
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedUtc = DateTime.UtcNow,
                    IsActive = true
                };

                _Collections[slug] = CloneCollection(collection);
                _Files[slug] = new Dictionary<string, FileRecord>(StringComparer.OrdinalIgnoreCase);
                return Task.FromResult(CloneCollection(collection));
            }
        }

        /// <inheritdoc />
        public Task DeleteCollectionAsync(string slug, CancellationToken token = default)
        {
            lock (_Sync)
            {
                if (!_Collections.Remove(slug))
                    throw new KeyNotFoundException("Collection with slug '" + slug + "' not found.");

                _Files.Remove(slug);
                return Task.CompletedTask;
            }
        }

        /// <inheritdoc />
        public Task<List<CollectionFile>> GetCollectionFilesAsync(string slug, CancellationToken token = default)
        {
            lock (_Sync)
            {
                EnsureCollection(slug);
                return Task.FromResult(_Files[slug].Select(kvp => ToCollectionFile(slug, kvp.Key, kvp.Value)).ToList());
            }
        }

        /// <inheritdoc />
        public async Task UploadFileAsync(string slug, string fileName, Stream content, string contentType, CancellationToken token = default)
        {
            using MemoryStream ms = new MemoryStream();
            await content.CopyToAsync(ms, token).ConfigureAwait(false);

            lock (_Sync)
            {
                EnsureCollection(slug);
                _Files[slug][fileName] = new FileRecord
                {
                    Content = ms.ToArray(),
                    ContentType = String.IsNullOrEmpty(contentType) ? Constants.BinaryContentType : contentType,
                    LastModifiedUtc = DateTime.UtcNow,
                    ETag = Guid.NewGuid().ToString("N")
                };
            }
        }

        /// <inheritdoc />
        public Task<DownloadFileResult> DownloadFileAsync(string slug, string fileName, CancellationToken token = default)
        {
            lock (_Sync)
            {
                EnsureCollection(slug);
                if (!_Files[slug].TryGetValue(fileName, out FileRecord record))
                    throw new FileNotFoundException("File '" + fileName + "' not found in collection '" + slug + "'.");

                return Task.FromResult(new DownloadFileResult
                {
                    Content = new MemoryStream(record.Content, writable: false),
                    ContentLength = record.Content.LongLength,
                    ContentType = record.ContentType,
                    FileName = fileName
                });
            }
        }

        /// <inheritdoc />
        public Task DeleteFileAsync(string slug, string fileName, CancellationToken token = default)
        {
            lock (_Sync)
            {
                EnsureCollection(slug);
                if (!_Files[slug].Remove(fileName))
                    throw new KeyNotFoundException("File '" + fileName + "' not found in collection '" + slug + "'.");

                return Task.CompletedTask;
            }
        }

        /// <inheritdoc />
        public Task<int> DeleteFilesAsync(string slug, List<string> fileNames, CancellationToken token = default)
        {
            lock (_Sync)
            {
                EnsureCollection(slug);
                foreach (string fileName in fileNames)
                {
                    _Files[slug].Remove(fileName);
                }

                return Task.FromResult(fileNames.Count);
            }
        }

        /// <inheritdoc />
        public Task<CollectionFile> GetFileMetadataAsync(string slug, string fileName, CancellationToken token = default)
        {
            lock (_Sync)
            {
                EnsureCollection(slug);
                if (!_Files[slug].TryGetValue(fileName, out FileRecord record))
                    throw new KeyNotFoundException("File '" + fileName + "' not found in collection '" + slug + "'.");

                return Task.FromResult(ToCollectionFile(slug, fileName, record));
            }
        }

        private void EnsureCollection(string slug)
        {
            if (!_Collections.ContainsKey(slug))
                throw new KeyNotFoundException("Collection with slug '" + slug + "' not found.");
        }

        private static Collection CloneCollection(Collection input)
        {
            return new Collection
            {
                Id = input.Id,
                Name = input.Name,
                Slug = input.Slug,
                CreatedUtc = input.CreatedUtc,
                IsActive = input.IsActive
            };
        }

        private static CollectionFile ToCollectionFile(string slug, string fileName, FileRecord record)
        {
            return new CollectionFile
            {
                Key = slug + "/" + fileName,
                FileName = fileName,
                Size = record.Content.LongLength,
                LastModifiedUtc = record.LastModifiedUtc,
                ContentType = record.ContentType,
                ETag = record.ETag
            };
        }

        private sealed class FileRecord
        {
            public byte[] Content { get; set; } = Array.Empty<byte>();
            public string ContentType { get; set; } = Constants.BinaryContentType;
            public DateTime LastModifiedUtc { get; set; } = DateTime.UtcNow;
            public string ETag { get; set; } = Guid.NewGuid().ToString("N");
        }
    }
}
