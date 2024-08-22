using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DeezNET.Data;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download.Clients.Deezer.Queue;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Download.Clients.Deezer
{
    public interface IDeezerProxy
    {
        List<DownloadClientItem> GetQueue(DeezerSettings settings);
        Task<string> Download(RemoteAlbum remoteAlbum, DeezerSettings settings);
        void RemoveFromQueue(string downloadId, DeezerSettings settings);
    }

    public class DeezerProxy : IDeezerProxy
    {
        private readonly ICached<DateTime?> _startTimeCache;
        private readonly DownloadTaskQueue _taskQueue;

        public DeezerProxy(ICacheManager cacheManager, Logger logger)
        {
            _startTimeCache = cacheManager.GetCache<DateTime?>(GetType(), "startTimes");
            _taskQueue = new(500, null, logger);

            _taskQueue.StartQueueHandler();
        }

        public List<DownloadClientItem> GetQueue(DeezerSettings settings)
        {
            _taskQueue.SetSettings(settings);

            var listing = _taskQueue.GetQueueListing();
            var completed = listing.Where(x => x.Status == DownloadItemStatus.Completed);
            var queue = listing.Where(x => x.Status == DownloadItemStatus.Queued);
            var current = listing.Where(x => x.Status == DownloadItemStatus.Downloading);

            var result = completed.Concat(current).Concat(queue).Where(x => x != null).Select(ToDownloadClientItem).ToList();

            return result;
        }

        public void RemoveFromQueue(string downloadId, DeezerSettings settings)
        {
            _taskQueue.SetSettings(settings);

            var item = _taskQueue.GetQueueListing().FirstOrDefault(a => a.ID == downloadId);
            if (item != null)
                _taskQueue.RemoveItem(item);
        }

        public async Task<string> Download(RemoteAlbum remoteAlbum, DeezerSettings settings)
        {
            _taskQueue.SetSettings(settings);

            var downloadItem = await DownloadItem.From(remoteAlbum);
            await _taskQueue.QueueBackgroundWorkItemAsync(downloadItem);
            return downloadItem.ID;
        }

        private DownloadClientItem ToDownloadClientItem(DownloadItem x)
        {
            var title = $"{x.Artist} - {x.Title} [WEB] {x.Bitrate}";
            if (x.Explicit)
            {
                title += " [Explicit]";
            }

            var item = new DownloadClientItem
            {
                DownloadId = x.ID,
                Title = title,
                TotalSize = x.TotalSize,
                RemainingSize = x.TotalSize - x.DownloadedSize,
                RemainingTime = GetRemainingTime(x),
                Status = x.Status,
                CanMoveFiles = true,
                CanBeRemoved = true,
            };

            if (x.DownloadFolder.IsNotNullOrWhiteSpace())
            {
                item.OutputPath = new OsPath(x.DownloadFolder);
            }

            return item;
        }

        private TimeSpan? GetRemainingTime(DownloadItem x)
        {
            if (x.Status == DownloadItemStatus.Completed)
            {
                _startTimeCache.Remove(x.ID);
                return null;
            }

            if (x.Progress == 0)
            {
                return null;
            }

            var started = _startTimeCache.Find(x.ID);
            if (started == null)
            {
                started = DateTime.UtcNow;
                _startTimeCache.Set(x.ID, started);
                return null;
            }

            var elapsed = DateTime.UtcNow - started;
            var progress = Math.Min(x.Progress, 1);

            return TimeSpan.FromTicks((long)(elapsed.Value.Ticks * (1 - progress) / progress));
        }
    }
}
