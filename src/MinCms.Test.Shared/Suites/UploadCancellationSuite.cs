namespace MinCms.Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using MinCms.Core;
    using MinCms.Core.Services;
    using MinCms.Test.Shared.Support;
    using Touchstone.Core;

    public static partial class MinCmsSuites
    {
        private const string _CancelBoundary = "cancel-test-boundary";
        private static readonly TimeSpan _CancelTimeout = TimeSpan.FromSeconds(20);

        /// <summary>A cancelled upload must leave nothing behind on the server or in storage.</summary>
        public static TestSuiteDescriptor UploadCancellationSuite()
        {
            const string s = "UploadCancellation";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                // ---------- Storage layer ----------
                Case(s, "S3.CancelDuringPartAborts", "Cancelling during a part aborts the multipart upload", async () =>
                {
                    using CancellationTokenSource cts = new CancellationTokenSource();
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter
                    {
                        BeforeUploadPart = part => { if (part == 2) cts.Cancel(); }
                    };

                    await UploadExpectingCancel(client, "cancelled.bin", cts.Token);

                    Check.Equal(1, client.AbortMultipartUploadCalls.Count);
                    Check.Equal(client.InitiateMultipartUploadCalls[0].UploadId, client.AbortMultipartUploadCalls[0].UploadId);
                    Check.Equal(0, client.CompleteMultipartUploadCalls.Count);
                    Check.False(client.Contains("alpha/cancelled.bin"), "no object should be stored");
                }),

                Case(s, "S3.CancelBeforeCompleteAborts", "Cancelling after the last part never completes the upload", async () =>
                {
                    using CancellationTokenSource cts = new CancellationTokenSource();
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter
                    {
                        AfterUploadPart = part => { if (part == 2) cts.Cancel(); }
                    };

                    await UploadExpectingCancel(client, "late.bin", cts.Token);

                    Check.Equal(1, client.AbortMultipartUploadCalls.Count);
                    Check.Equal(0, client.CompleteMultipartUploadCalls.Count);
                    Check.False(client.Contains("alpha/late.bin"), "no object should be stored");
                }),

                Case(s, "S3.CancelDuringCompleteRemovesNewObject", "Cancelling during completion removes a newly created object", async () =>
                {
                    using CancellationTokenSource cts = new CancellationTokenSource();
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter { DuringCompleteMultipartUpload = () => cts.Cancel() };

                    await UploadExpectingCancel(client, "new.bin", cts.Token);

                    Check.False(client.Contains("alpha/new.bin"), "the completed object should be deleted");
                    Check.Equal(0, client.AbortMultipartUploadCalls.Count);
                }),

                Case(s, "S3.CancelDuringCompleteKeepsReplacement", "Cancelling during completion of a replacement never deletes the file", async () =>
                {
                    using CancellationTokenSource cts = new CancellationTokenSource();
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter { DuringCompleteMultipartUpload = () => cts.Cancel() };
                    client.Seed("alpha/existing.bin", new byte[] { 1, 2, 3 }, "application/octet-stream");

                    await UploadExpectingCancel(client, "existing.bin", cts.Token);

                    Check.True(client.Contains("alpha/existing.bin"), "the file must not be lost");
                    Check.False(client.DeleteObjectCalls.Contains("alpha/existing.bin"), "must not delete the only remaining copy");
                }),

                Case(s, "S3.CancelledSmallUploadStoresNothing", "A cancelled small upload stores nothing", async () =>
                {
                    using CancellationTokenSource cts = new CancellationTokenSource();
                    cts.Cancel();
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);

                    await Check.ThrowsAsync<OperationCanceledException>(() =>
                        service.UploadFileAsync("alpha", "small.txt", new MemoryStream(CreatePayload(1024), writable: false), "text/plain", cts.Token));

                    Check.False(client.Contains("alpha/small.txt"), "no object should be stored");
                }),

                Case(s, "S3.UncancelledUploadUnchanged", "An uninterrupted multipart upload stores the object and aborts nothing", async () =>
                {
                    FakeS3ClientAdapter client = new FakeS3ClientAdapter();
                    S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
                    using CancellationTokenSource cts = new CancellationTokenSource();

                    await service.UploadFileAsync("alpha", "fine.bin", new MemoryStream(CreatePayload((6 * 1024 * 1024) + 17), writable: false), "application/octet-stream", cts.Token);

                    Check.True(client.Contains("alpha/fine.bin"), "object should be stored");
                    Check.Equal(0, client.AbortMultipartUploadCalls.Count);
                    Check.Equal(0, client.DeleteObjectCalls.Count);
                }),

                // ---------- HTTP host ----------
                Case(s, "Host.DisconnectMidBody", "Disconnecting while sending the file stores nothing and removes temp files", () =>
                    DisconnectMidBody(chunked: false, reset: false)),

                Case(s, "Host.ResetMidBody", "A connection reset while sending the file stores nothing and removes temp files", () =>
                    DisconnectMidBody(chunked: false, reset: true)),

                Case(s, "Host.DisconnectMidChunkedBody", "Disconnecting during a chunked upload stores nothing and removes temp files", () =>
                    DisconnectMidBody(chunked: true, reset: false)),

                Case(s, "Host.DisconnectDuringStorage", "Disconnecting while the server stores the file cancels the storage work", () =>
                    DisconnectDuringStorage(reset: false)),

                Case(s, "Host.ResetDuringStorage", "A connection reset while the server stores the file cancels the storage work", () =>
                    DisconnectDuringStorage(reset: true)),

                Case(s, "Host.CompleteUploadUnaffected", "A connected client's slow upload is not cancelled", async () =>
                {
                    InMemoryCollectionService service = new InMemoryCollectionService
                    {
                        UploadStorageDelay = token => Task.Delay(TimeSpan.FromSeconds(3), token)
                    };
                    using ApiHostContext ctx = ApiHost.CreateIsolated(null, service);

                    using HttpRequestMessage upload = ApiHost.Authorized(HttpMethod.Post, "/v1.0/collections/alpha/files");
                    upload.Content = FileUpload(CreatePayload(4096), "slow.bin", "application/octet-stream");
                    using HttpResponseMessage response = await ctx.Client.SendAsync(upload);

                    Check.Equal(HttpStatusCode.Created, response.StatusCode);
                    Check.True(await StoredIn(service, "slow.bin"), "the file should be stored");
                })
            };

            return new TestSuiteDescriptor(s, "Upload cancellation cleanup", cases);
        }

        private static async Task UploadExpectingCancel(FakeS3ClientAdapter client, string fileName, CancellationToken token)
        {
            S3Service service = new S3Service(CreateS3Settings(), QuietLogging(), client);
            byte[] payload = CreatePayload((6 * 1024 * 1024) + 17);
            await Check.ThrowsAsync<OperationCanceledException>(() =>
                service.UploadFileAsync("alpha", fileName, new MemoryStream(payload, writable: false), "application/octet-stream", token));
        }

        private static async Task DisconnectMidBody(bool chunked, bool reset)
        {
            HashSet<string> tempBefore = MinCmsTempFiles();
            InMemoryCollectionService service = new InMemoryCollectionService();
            using ApiHostContext ctx = ApiHost.CreateIsolated(null, service);

            string partHeader = "--" + _CancelBoundary + "\r\nContent-Disposition: form-data; name=\"file\"; filename=\"partial.bin\"\r\nContent-Type: application/octet-stream\r\n\r\n";
            byte[] partial = Encoding.ASCII.GetBytes(partHeader).Concat(CreatePayload(512 * 1024)).ToArray();

            using (TcpClient client = await ConnectRaw(ctx))
            {
                NetworkStream stream = client.GetStream();
                string framing = chunked ? "Transfer-Encoding: chunked\r\n" : "Content-Length: " + (partial.Length * 4) + "\r\n";
                await stream.WriteAsync(UploadHeaders(ctx, framing));

                if (chunked)
                {
                    await stream.WriteAsync(Encoding.ASCII.GetBytes(partial.Length.ToString("x") + "\r\n"));
                    await stream.WriteAsync(partial);
                    await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n"));
                }
                else
                {
                    await stream.WriteAsync(partial);
                }

                await stream.FlushAsync();
                await Task.Delay(500);
                await WaitForNewTempFile(tempBefore);
                if (reset) client.LingerState = new LingerOption(true, 0);
            }

            await WaitForTempCleanup(tempBefore);
            Check.False(await StoredIn(service, "partial.bin"), "a partial upload must not be stored");
            await AssertHealthy(ctx);
        }

        private static async Task DisconnectDuringStorage(bool reset)
        {
            HashSet<string> tempBefore = MinCmsTempFiles();
            TaskCompletionSource storing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource cancelled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            InMemoryCollectionService service = new InMemoryCollectionService
            {
                UploadStorageDelay = async token =>
                {
                    using CancellationTokenRegistration registration = token.Register(() => cancelled.TrySetResult());
                    storing.TrySetResult();
                    await Task.Delay(TimeSpan.FromSeconds(60), token).ConfigureAwait(false);
                }
            };
            using ApiHostContext ctx = ApiHost.CreateIsolated(null, service);

            byte[] body = Encoding.ASCII.GetBytes(
                "--" + _CancelBoundary + "\r\nContent-Disposition: form-data; name=\"file\"; filename=\"storing.bin\"\r\nContent-Type: application/octet-stream\r\n\r\n")
                .Concat(CreatePayload(64 * 1024))
                .Concat(Encoding.ASCII.GetBytes("\r\n--" + _CancelBoundary + "--\r\n"))
                .ToArray();

            using (TcpClient client = await ConnectRaw(ctx))
            {
                NetworkStream stream = client.GetStream();
                await stream.WriteAsync(UploadHeaders(ctx, "Content-Length: " + body.Length + "\r\n"));
                await stream.WriteAsync(body);
                await stream.FlushAsync();
                await storing.Task.WaitAsync(_CancelTimeout);
                if (reset) client.LingerState = new LingerOption(true, 0);
            }

            await cancelled.Task.WaitAsync(_CancelTimeout);
            await WaitForTempCleanup(tempBefore);
            Check.False(await StoredIn(service, "storing.bin"), "a cancelled upload must not be stored");
            await AssertHealthy(ctx);
        }

        private static async Task<TcpClient> ConnectRaw(ApiHostContext ctx)
        {
            TcpClient client = new TcpClient();
            await client.ConnectAsync(ctx.Client.BaseAddress.Host, ctx.Client.BaseAddress.Port);
            return client;
        }

        private static byte[] UploadHeaders(ApiHostContext ctx, string framing)
        {
            return Encoding.ASCII.GetBytes(
                "POST /v1.0/collections/alpha/files HTTP/1.1\r\n"
                + "Host: " + ctx.Client.BaseAddress.Authority + "\r\n"
                + "x-api-key: " + ApiHost.ApiKey + "\r\n"
                + "Content-Type: multipart/form-data; boundary=" + _CancelBoundary + "\r\n"
                + framing
                + "\r\n");
        }

        private static async Task<bool> StoredIn(InMemoryCollectionService service, string fileName)
        {
            List<CollectionFile> files = await service.GetCollectionFilesAsync("alpha");
            return files.Any(f => f.FileName == fileName);
        }

        private static async Task AssertHealthy(ApiHostContext ctx)
        {
            using HttpResponseMessage response = await ctx.Client.GetAsync("/download/alpha/sample.txt");
            Check.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        private static HashSet<string> MinCmsTempFiles()
        {
            return Directory.GetFiles(Path.GetTempPath(), "mincms-*").ToHashSet();
        }

        private static async Task WaitForNewTempFile(HashSet<string> before)
        {
            DateTime deadline = DateTime.UtcNow + _CancelTimeout;
            while (DateTime.UtcNow < deadline)
            {
                if (MinCmsTempFiles().Except(before).Any()) return;
                await Task.Delay(25);
            }

            throw new Exception("The server never created a temp file, so the test did not exercise cleanup.");
        }

        private static async Task WaitForTempCleanup(HashSet<string> before)
        {
            DateTime deadline = DateTime.UtcNow + _CancelTimeout;
            List<string> leftover = new List<string>();
            while (DateTime.UtcNow < deadline)
            {
                leftover = MinCmsTempFiles().Except(before).ToList();
                if (leftover.Count == 0) return;
                await Task.Delay(50);
            }

            throw new Exception("Temp files were left behind: " + String.Join(", ", leftover));
        }
    }
}
