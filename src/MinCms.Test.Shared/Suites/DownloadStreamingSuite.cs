namespace MinCms.Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using MinCms.Test.Shared.Support;
    using Touchstone.Core;

    public static partial class MinCmsSuites
    {
        private static readonly TimeSpan _StreamingTimeout = TimeSpan.FromSeconds(15);

        /// <summary>Download streaming behavior and large-transfer regressions on the HTTP host.</summary>
        public static TestSuiteDescriptor DownloadStreamingSuite()
        {
            const string s = "DownloadStreaming";
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>
            {
                Case(s, "Download.FirstBytesBeforeSourceFinished", "Client receives data while storage is still being read", async () =>
                {
                    const int gateAfter = 256 * 1024;
                    const int total = 1024 * 1024;
                    ControlledReadStream source = new ControlledReadStream(total, gateAfter: gateAfter);
                    using ApiHostContext ctx = HostServing(source, total);
                    using CancellationTokenSource timeout = new CancellationTokenSource(_StreamingTimeout);

                    try
                    {
                        using HttpResponseMessage response = await ctx.Client.GetAsync("/download/alpha/big.bin", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                        Check.Equal(HttpStatusCode.OK, response.StatusCode);
                        Check.Equal((long?)total, response.Content.Headers.ContentLength);

                        using Stream body = await response.Content.ReadAsStreamAsync(timeout.Token);
                        byte[] first = new byte[1024];
                        await body.ReadExactlyAsync(first, timeout.Token);
                        AssertPattern(first, 0);

                        Check.True(source.BytesConsumed <= gateAfter, "the source must still be held at its gate");
                        source.Release();

                        byte[] rest = new byte[total - first.Length];
                        await body.ReadExactlyAsync(rest, timeout.Token);
                        AssertPattern(rest, first.Length);
                    }
                    finally
                    {
                        source.Release();
                    }
                }),

                Case(s, "Download.LargeBinaryIntact", "A large non-seekable object arrives byte-exact with its headers", async () =>
                {
                    const int total = 16 * 1024 * 1024;
                    ControlledReadStream source = new ControlledReadStream(total);
                    using ApiHostContext ctx = HostServing(source, total);

                    using HttpResponseMessage response = await ctx.Client.GetAsync("/download/alpha/folder%2Fbig.bin");
                    Check.Equal(HttpStatusCode.OK, response.StatusCode);
                    Check.Equal((long?)total, response.Content.Headers.ContentLength);
                    Check.Equal("application/octet-stream", response.Content.Headers.ContentType?.MediaType);
                    Check.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
                    Check.Equal("big.bin", response.Content.Headers.ContentDisposition?.FileName?.Trim('"'));

                    byte[] body = await response.Content.ReadAsByteArrayAsync();
                    Check.Equal(total, body.Length);
                    AssertPattern(body, 0);
                    await source.DisposedTask.WaitAsync(_StreamingTimeout);
                }),

                Case(s, "Download.EmptyFile", "A zero-byte object downloads as an empty 200", async () =>
                {
                    ControlledReadStream source = new ControlledReadStream(0);
                    using ApiHostContext ctx = HostServing(source, 0);

                    using HttpResponseMessage response = await ctx.Client.GetAsync("/download/alpha/empty.bin");
                    Check.Equal(HttpStatusCode.OK, response.StatusCode);
                    Check.Equal((long?)0, response.Content.Headers.ContentLength);
                    Check.Equal(0, (await response.Content.ReadAsByteArrayAsync()).Length);
                    await source.DisposedTask.WaitAsync(_StreamingTimeout);
                }),

                Case(s, "Download.StorageFailureIsDetectable", "A storage failure mid-transfer is visible to the client and leaves the host healthy", async () =>
                {
                    const int total = 4 * 1024 * 1024;
                    const int failAfter = 512 * 1024;
                    ControlledReadStream source = new ControlledReadStream(total, failAfter: failAfter);
                    using ApiHostContext ctx = HostServing(source, total, onlyFor: "broken.bin");
                    using CancellationTokenSource timeout = new CancellationTokenSource(_StreamingTimeout);

                    bool detected = false;
                    byte[] received = new byte[total];
                    int offset = 0;

                    try
                    {
                        using HttpResponseMessage response = await ctx.Client.GetAsync("/download/alpha/broken.bin", HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                        using Stream body = await response.Content.ReadAsStreamAsync(timeout.Token);

                        int read;
                        while (offset < total && (read = await body.ReadAsync(received.AsMemory(offset), timeout.Token)) > 0)
                        {
                            offset += read;
                        }

                        detected = offset < total;
                    }
                    catch (Exception e) when (e is HttpRequestException || e is IOException)
                    {
                        detected = true;
                    }

                    Check.True(detected, "a truncated transfer must not look complete");
                    Check.True(offset <= failAfter, "no bytes beyond the failure point may be sent, got " + offset);
                    AssertPattern(received.AsSpan(0, offset).ToArray(), 0);
                    await source.DisposedTask.WaitAsync(_StreamingTimeout);

                    using HttpResponseMessage healthy = await ctx.Client.GetAsync("/download/alpha/sample.txt", timeout.Token);
                    Check.Equal(HttpStatusCode.OK, healthy.StatusCode);
                    Check.Equal("seed file", await healthy.Content.ReadAsStringAsync(timeout.Token));
                }),

                Case(s, "Download.ClientDisconnectStopsReading", "A client that disconnects early stops the storage read and releases the stream", async () =>
                {
                    const long total = 1024L * 1024L * 1024L;
                    ControlledReadStream source = new ControlledReadStream(total);
                    using ApiHostContext ctx = HostServing(source, total, onlyFor: "huge.bin");
                    using CancellationTokenSource timeout = new CancellationTokenSource(_StreamingTimeout);

                    using (HttpClient isolatedClient = new HttpClient { BaseAddress = ctx.Client.BaseAddress })
                    using (HttpResponseMessage response = await isolatedClient.GetAsync("/download/alpha/huge.bin", HttpCompletionOption.ResponseHeadersRead, timeout.Token))
                    using (Stream body = await response.Content.ReadAsStreamAsync(timeout.Token))
                    {
                        byte[] first = new byte[64 * 1024];
                        await body.ReadExactlyAsync(first, timeout.Token);
                        AssertPattern(first, 0);
                    }

                    await source.DisposedTask.WaitAsync(_StreamingTimeout);
                    Check.True(source.BytesConsumed < total / 2, "the server should stop reading, consumed " + source.BytesConsumed);

                    using HttpResponseMessage healthy = await ctx.Client.GetAsync("/download/alpha/sample.txt", timeout.Token);
                    Check.Equal(HttpStatusCode.OK, healthy.StatusCode);
                }),

                Case(s, "Download.ConcurrentIntact", "Concurrent downloads do not interfere with each other", async () =>
                {
                    const int total = 4 * 1024 * 1024;
                    InMemoryCollectionService service = new InMemoryCollectionService
                    {
                        DownloadOverride = name => (new ControlledReadStream(total), (long)total)
                    };
                    using ApiHostContext ctx = ApiHost.CreateIsolated(null, service);

                    byte[][] bodies = await Task.WhenAll(Enumerable.Range(0, 6)
                        .Select(i => ctx.Client.GetByteArrayAsync("/download/alpha/file" + i + ".bin")));

                    foreach (byte[] body in bodies)
                    {
                        Check.Equal(total, body.Length);
                        AssertPattern(body, 0);
                    }
                }),

                Case(s, "Download.CorsHeadersPreserved", "Downloads still carry CORS headers for the dashboard's fetch calls", async () =>
                {
                    using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "/download/alpha/sample.txt");
                    request.Headers.Add("Origin", "http://dashboard.example");
                    using HttpResponseMessage response = await ApiHost.Client.SendAsync(request);
                    Check.Equal(HttpStatusCode.OK, response.StatusCode);
                    Check.True(response.Headers.Contains("Access-Control-Allow-Origin"), "missing Access-Control-Allow-Origin");
                }),

                Case(s, "Download.RoundTripBinary", "A binary upload downloads byte-identical, including boundary-like content", async () =>
                {
                    using ApiHostContext ctx = ApiHost.CreateIsolated(null);
                    byte[] payload = CreatePayload(3 * 1024 * 1024);
                    byte[] lookalike = System.Text.Encoding.ASCII.GetBytes("\r\n--not-the-boundary\r\n");
                    Buffer.BlockCopy(lookalike, 0, payload, 65530, lookalike.Length);

                    using HttpRequestMessage upload = ApiHost.Authorized(HttpMethod.Post, "/v1.0/collections/alpha/files");
                    upload.Content = FileUpload(payload, "round-trip.bin", "application/octet-stream");
                    using HttpResponseMessage uploaded = await ctx.Client.SendAsync(upload);
                    Check.Equal(HttpStatusCode.Created, uploaded.StatusCode);

                    byte[] downloaded = await ctx.Client.GetByteArrayAsync("/download/alpha/round-trip.bin");
                    Check.BytesEqual(payload, downloaded);
                })
            };

            return new TestSuiteDescriptor(s, "Download streaming behavior", cases);
        }

        private static ApiHostContext HostServing(Stream source, long length, string onlyFor = null)
        {
            InMemoryCollectionService service = new InMemoryCollectionService
            {
                DownloadOverride = name =>
                {
                    if (onlyFor != null && name != onlyFor) return null;
                    return (source, length);
                }
            };

            return ApiHost.CreateIsolated(null, service);
        }

        private static void AssertPattern(byte[] data, long startPosition)
        {
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i] != ControlledReadStream.ByteAt(startPosition + i))
                {
                    throw new Exception("Byte mismatch at position " + (startPosition + i));
                }
            }
        }
    }
}
