using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Common.Serializer;
using NzbDrone.Core.Indexers.Deezer;

namespace NzbDrone.Core.Download.Clients.Deezer
{
    public interface IDeezerProxy
    {
        List<DownloadClientItem> GetQueue(DeezerSettings settings);
        string Download(string url, int bitrate, DeezerSettings settings);
        void RemoveFromQueue(string downloadId, DeezerSettings settings);
    }

    public class DeezerProxy : IDeezerProxy
    {
        private static readonly Dictionary<string, long> Bitrates = new Dictionary<string, long>
        {
            { "1", 128 },
            { "3", 320 },
            { "9", 1000 }
        };
        private static readonly Dictionary<string, string> Formats = new Dictionary<string, string>
        {
            { "1", "MP3 128" },
            { "3", "MP3 320" },
            { "9", "FLAC" }
        };

        private readonly ICached<string> _sessionCookieCache;
        private readonly ICached<DateTime?> _startTimeCache;
        private readonly ICached<DeezerUser> _userCache;
        private readonly IHttpClient _httpClient;
        private readonly Logger _logger;

        private double _bytesPerSecond = 0;

        public DeezerProxy(ICacheManager cacheManager,
            IHttpClient httpClient,
            Logger logger)
        {
            _sessionCookieCache = cacheManager.GetCache<string>(GetType(), "sessionCookies");
            _startTimeCache = cacheManager.GetCache<DateTime?>(GetType(), "startTimes");
            _userCache = cacheManager.GetCache<DeezerUser>(GetType(), "user");
            _httpClient = httpClient;
            _logger = logger;
        }

        public List<DownloadClientItem> GetQueue(DeezerSettings settings)
        {
            // TODO: write queue system

            /*var request = BuildRequest(settings).Resource("/api/getQueue");
            var response = ProcessRequest<DeezerQueue>(request);

            var completed = response.Queue.Values.Where(x => x.Status == "completed");
            var queue = response.Queue.Values.Where(x => x.Status == "inQueue").OrderBy(x => response.QueueOrder.IndexOf(x.Id));
            var current = response.Current;

            var result = completed.Concat(new[] { current }).Concat(queue).Where(x => x != null).Select(ToDownloadClientItem).ToList();

            var currentItem = result.FirstOrDefault(x => x.Status == DownloadItemStatus.Downloading);

            if (currentItem != null && currentItem.RemainingTime.HasValue)
            {
                var remainingTime = currentItem.RemainingTime.Value;

                foreach (var item in result)
                {
                    if (item.Status == DownloadItemStatus.Queued)
                    {
                        remainingTime += TimeSpan.FromSeconds(item.TotalSize / _bytesPerSecond);
                        item.RemainingTime = remainingTime;
                    }
                }
            }

            return result;*/
            return [];
        }

        public void RemoveFromQueue(string downloadId, DeezerSettings settings)
        {
            // TODO: write queue system

            /*var request = BuildRequest(settings)
                .Resource("/api/removeFromQueue")
                .Post()
                .AddQueryParam("uuid", downloadId);

            ProcessRequest(request);*/
        }

        public string Download(string url, int bitrate, DeezerSettings settings)
        {
            // TODO: write download system

            /*Authenticate(settings);

            var request = BuildRequest(settings)
                .Resource("/api/addToQueue")
                .Post()
                .AddFormParameter("url", url)
                .AddFormParameter("bitrate", bitrate);

            var response = ProcessRequest<DeezerResult<DeezerAddResult>>(request);

            if (response.Result)
            {
                if (response.Data.Obj.Count != 1)
                {
                    throw new DownloadClientException("Expected Deezer to add 1 item, got {0}", response.Data.Obj.Count);
                }

                _logger.Trace("Downloading item {0}", response.Data.Obj[0].Uuid);
                return response.Data.Obj[0].Uuid;
            }

            throw new DownloadClientException("Error adding item to Deezer: {0}", response.Errid);*/
            return "";
        }

        private DownloadClientItem ToDownloadClientItem(DeezerQueueItem x)
        {
            var title = $"{x.Artist} - {x.Title} [WEB] {Formats[x.Bitrate]}";
            if (x.Explicit)
            {
                title += " [Explicit]";
            }

            // assume 3 mins per track, bitrates in kbps
            var size = x.Size * 180L * Bitrates[x.Bitrate] * 128L;

            var item = new DownloadClientItem
            {
                DownloadId = x.Uuid,
                Title = title,
                TotalSize = size,
                RemainingSize = (long)((1 - (x.Progress / 100.0)) * size),
                RemainingTime = GetRemainingTime(x, size),
                Status = GetItemStatus(x),
                CanMoveFiles = true,
                CanBeRemoved = true
            };

            if (x.ExtrasPath.IsNotNullOrWhiteSpace())
            {
                item.OutputPath = new OsPath(x.ExtrasPath);
            }

            return item;
        }

        private static DownloadItemStatus GetItemStatus(DeezerQueueItem item)
        {
            if (item.Failed > 0)
            {
                return DownloadItemStatus.Failed;
            }

            if (item.Status == "inQueue")
            {
                return DownloadItemStatus.Queued;
            }

            if (item.Status == "completed")
            {
                return DownloadItemStatus.Completed;
            }

            if (item.Progress is > 0 and < 100)
            {
                return DownloadItemStatus.Downloading;
            }

            return DownloadItemStatus.Queued;
        }

        private TimeSpan? GetRemainingTime(DeezerQueueItem x, long size)
        {
            if (x.Progress == 100)
            {
                _startTimeCache.Remove(x.Id);
                return null;
            }

            if (x.Progress == 0)
            {
                return null;
            }

            var started = _startTimeCache.Find(x.Id);
            if (started == null)
            {
                started = DateTime.UtcNow;
                _startTimeCache.Set(x.Id, started);
                return null;
            }

            var elapsed = DateTime.UtcNow - started;
            var progress = Math.Min(x.Progress, 100) / 100.0;

            _bytesPerSecond = (progress * size) / elapsed.Value.TotalSeconds;

            return TimeSpan.FromTicks((long)(elapsed.Value.Ticks * (1 - progress) / progress));
        }
    }
}
