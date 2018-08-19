﻿using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AppGet.FileSystem;
using AppGet.Http;
using AppGet.ProgressTracker;

namespace AppGet.FileTransfer.Protocols
{
    public class HttpFileTransferClient : IFileTransferClient
    {
        private static readonly Regex HttpRegex = new Regex(@"^https?\:\/\/", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex FileNameRegex = new Regex(@"\.(zip|7zip|7z|rar|msi|exe)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private readonly IFileSystem _fileSystem;
        private readonly IHttpClient _httpClient;

        public HttpFileTransferClient(IHttpClient httpClient, IFileSystem fileSystem)
        {
            _httpClient = httpClient;
            _fileSystem = fileSystem;
        }

        public bool CanHandleProtocol(string source)
        {
            return HttpRegex.IsMatch(source);
        }

        public async Task<string> GetFileName(string source)
        {
            var uri = new Uri(source);

            var fileName = Path.GetFileName(uri.LocalPath);

            if (FileNameRegex.IsMatch(fileName)) return fileName;

            var resp = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);

            if (resp.RequestMessage.RequestUri != uri) return await GetFileName(resp.RequestMessage.RequestUri.ToString());

            if (resp.Content.Headers.ContentDisposition != null) return resp.Content.Headers.ContentDisposition.FileName.Trim('"', '\'', ' ');

            throw new InvalidDownloadUrlException(source);
        }

        public async Task TransferFile(string source, string destinationFile)
        {
            using (var resp = await _httpClient.GetAsync(new Uri(source), HttpCompletionOption.ResponseHeadersRead))
            {
                if (resp.Content.Headers.ContentType.MediaType.Contains("text"))
                    throw new InvalidDownloadUrlException(source, $"[ContentType={resp.Content.Headers.ContentType.MediaType}]");

                if (_fileSystem.FileExists(destinationFile)) _fileSystem.DeleteFile(destinationFile);

                var progress = new ProgressState
                {
                    MaxValue = resp.Content.Headers.ContentLength
                };

                using (var httpStream = await resp.Content.ReadAsStreamAsync())
                {
                    var tempFile = $"{destinationFile}.APPGET_DOWNLOAD";
                    int len;

                    using (var tempFileStream = _fileSystem.Open(tempFile, FileMode.Create, FileAccess.ReadWrite))
                    {
                        var buffer = new byte[8 * 1024];
                        while ((len = httpStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            tempFileStream.Write(buffer, 0, len);
                            progress.Value += len;
                            OnStatusUpdated(progress);
                        }
                    }

                    progress.IsCompleted = true;
                    OnStatusUpdated(progress);
                    _fileSystem.Move(tempFile, destinationFile);
                }
            }
        }

        public async Task<string> ReadString(string source)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, $"{source}?cache={DateTime.Now.Ticks}");
            req.Headers.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true
            };

            var resp = await _httpClient.SendAsync(req);

            return await resp.Content.ReadAsString();
        }

        public Action<ProgressState> OnStatusUpdated { get; set; }
    }
}