using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using NLog;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Common.Instrumentation.Extensions;

namespace NzbDrone.Core.Download.Clients.Deezer.Queue
{
    public class DownloadTaskQueue
    {
        private readonly Channel<DownloadItem> _queue;
        private readonly List<DownloadItem> _items;
        private readonly Dictionary<DownloadItem, CancellationTokenSource> _cancellationSources;

        private readonly List<Task> _runningTasks = new();
        private readonly object _lock = new();

        private DeezerSettings _settings;
        private readonly Logger _logger;

        public DownloadTaskQueue(int capacity, DeezerSettings settings, Logger logger)
        {
            BoundedChannelOptions options = new(capacity)
            {
                FullMode = BoundedChannelFullMode.Wait
            };
            _queue = Channel.CreateBounded<DownloadItem>(options);
            _items = new();
            _cancellationSources = new();
            _settings = settings;
            _logger = logger;
        }

        public void SetSettings(DeezerSettings settings) => _settings = settings;

        public void StartQueueHandler()
        {
            Task.Run(() => BackgroundProcessing());
        }

        private async Task BackgroundProcessing(CancellationToken stoppingToken = default)
        {
            using SemaphoreSlim semaphore = new(3, 3);

            async Task HandleTask(DownloadItem item, Task task)
            {
                try
                {
                    var token = GetTokenForItem(item);
                    item.EnsureValidity();
                    item.Status = DownloadItemStatus.Downloading;
                    await task;
                }
                catch (TaskCanceledException) { }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    item.Status = DownloadItemStatus.Failed;
                    _logger.Error("Error while downloading Deezer album " + item.Title);
                    _logger.Error(ex.ToString());
                }
                finally
                {
                    semaphore.Release();
                    lock (_lock)
                        _runningTasks.Remove(task);
                }
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                await semaphore.WaitAsync(stoppingToken);

                var item = await DequeueAsync(stoppingToken);
                var token = GetTokenForItem(item);
                var downloadTask = item.DoDownload(_settings, _logger, token);

                lock (_lock)
                    _runningTasks.Add(HandleTask(item, downloadTask));
            }

            List<Task> remainingTasks;
            lock (_lock)
                remainingTasks = _runningTasks.ToList();
            await Task.WhenAll(remainingTasks);
        }

        public async ValueTask QueueBackgroundWorkItemAsync(DownloadItem workItem)
        {
            await _queue.Writer.WriteAsync(workItem);
            CancellationTokenSource token = new();
            _items.Add(workItem);
            _cancellationSources.Add(workItem, token);
        }

        private async ValueTask<DownloadItem> DequeueAsync(CancellationToken cancellationToken)
        {
            var workItem = await _queue.Reader.ReadAsync(cancellationToken);
            return workItem;
        }

        public void RemoveItem(DownloadItem workItem)
        {
            if (workItem == null)
                return;

            _cancellationSources[workItem].Cancel();

            _items.Remove(workItem);
            _cancellationSources.Remove(workItem);
        }

        public DownloadItem[] GetQueueListing()
        {
            return _items.ToArray();
        }

        public CancellationToken GetTokenForItem(DownloadItem item)
        {
            if (_cancellationSources.TryGetValue(item, out var src))
                return src!.Token;

            return default;
        }
    }
}
