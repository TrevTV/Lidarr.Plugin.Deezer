using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeezNET;
using DeezNET.Data;
using DeezNET.Exceptions;
using Newtonsoft.Json.Linq;
using NLog;
using NzbDrone.Plugin.Deezer;

namespace NzbDrone.Core.Download.Clients.Deezer.Queue
{
    public class DownloadItem
    {
        public static async Task<DownloadItem> From(string url, Bitrate bitrate)
        {
            url = url.Trim();

            DownloadItem item = null;
            if (url.Contains("deezer", StringComparison.CurrentCultureIgnoreCase))
            {
                if (DeezerURL.TryParse(url, out var deezerUrl))
                {
                    item = new()
                    {
                        ID = Guid.NewGuid().ToString(),
                        Status = DownloadItemStatus.Queued,
                        Bitrate = bitrate,
                        _deezerUrl = deezerUrl,
                    };

                    await item.SetDeezerData();
                }
            }

            return item;
        }

        public string ID { get; private set; }

        public string Title { get; private set; }
        public string Artist { get; private set; }
        public bool Explicit { get; private set; }

        public string DownloadFolder { get; private set; }

        public Bitrate Bitrate { get; private set; }
        public DownloadItemStatus Status { get; set; }

        public float Progress { get => DownloadedSize / (float)Math.Max(TotalSize, 1); }
        public long DownloadedSize { get; private set; }
        public long TotalSize { get; private set; }

        public int FailedTracks { get; private set; }

        private (long id, long size)[] _tracks;
        private DeezerURL _deezerUrl;
        private DateTime _lastARLValidityCheck = DateTime.MinValue;

        private readonly byte[] FLAC_MAGIC = Encoding.ASCII.GetBytes("fLaC");

        public async Task DoDownload(DeezerSettings settings, Logger logger, CancellationToken cancellation = default)
        {
            List<Task> tasks = new();
            using SemaphoreSlim semaphore = new(3, 3);
            foreach (var track in _tracks)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellation);

                    try
                    {
                        await DoTrackDownload(track.id, settings, cancellation);
                        DownloadedSize += track.size;
                    }
                    catch (TaskCanceledException) { }
                    catch (Exception ex)
                    {
                        logger.Error("Error while downloading Deezer track " + track, ex);
                        FailedTracks++;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellation));
            }

            await Task.WhenAll(tasks);
            if (FailedTracks > 0)
                Status = DownloadItemStatus.Failed;
            else
                Status = DownloadItemStatus.Completed;
        }

        private async Task DoTrackDownload(long track, DeezerSettings settings, CancellationToken cancellation = default)
        {
            JToken page = await DeezerAPI.Instance.Client.GWApi.GetTrackPage(track, cancellation);

            JToken albumPage = await DeezerAPI.Instance.Client.GWApi.GetAlbumPage(page["DATA"]!["ALB_ID"]!.Value<long>(), cancellation);

            byte[] trackData = await DeezerAPI.Instance.Client.Downloader.GetRawTrackBytes(track, Bitrate, null, cancellation);
            string extension = Enumerable.SequenceEqual(trackData[0..4], FLAC_MAGIC) ? "flac" : "mp3";

            trackData = await DeezerAPI.Instance.Client.Downloader.ApplyMetadataToTrackBytes(track, trackData, token: cancellation);

            string outPath = Path.Combine(settings.DownloadPath, MetadataUtilities.GetFilledTemplate("%albumartist%/%album%/", extension, page, albumPage));
            DownloadFolder = outPath;
            if (!Directory.Exists(outPath))
                Directory.CreateDirectory(outPath);

            try
            {
                string artOut = Path.Combine(outPath, "folder.jpg");
                if (!File.Exists(artOut))
                {
                    byte[] bigArt = await DeezerAPI.Instance.Client.Downloader.GetArtBytes(page["DATA"]!["ALB_PICTURE"]!.ToString(), 1024, cancellation);
                    await File.WriteAllBytesAsync(artOut, bigArt, cancellation);
                }
            }
            catch (UnavailableArtException) { }

            outPath = Path.Combine(outPath, MetadataUtilities.GetFilledTemplate("%track% - %title%.%ext%", extension, page, albumPage));

            await File.WriteAllBytesAsync(outPath, trackData, cancellation);
        }

        public void EnsureValidity()
        {
            if ((DateTime.Now - _lastARLValidityCheck).Minutes > 30)
            {
                // TODO: validity check, not sure if this works as intended
                /*var safeToWork = DeezerAPI.Instance.CheckAndSetARL(DeezerAPI.Instance.Client.ActiveARL);
                if (!safeToWork)
                    throw new InvalidARLException("No valid ARLs are available.");*/
            }
        }

        private async Task SetDeezerData(CancellationToken cancellation = default)
        {
            if (_deezerUrl.EntityType != EntityType.Album)
                throw new InvalidOperationException();

            var albumPage = await DeezerAPI.Instance.Client.GWApi.GetAlbumPage(_deezerUrl.Id, cancellation);

            string filesizeKey = Bitrate switch
            {
                Bitrate.MP3_128 => "FILESIZE_MP3_128",
                Bitrate.MP3_320 => "FILESIZE_MP3_320",
                Bitrate.FLAC => "FILESIZE_FLAC",
                _ => "FILESIZE"
            };

            _tracks ??= albumPage["SONGS"]!["data"]!.Select(t => (t["SNG_ID"]!.Value<long>(), t[filesizeKey]!.Value<long>())).ToArray();

            var album = albumPage["DATA"]!.ToObject<DeezerGwAlbum>();

            Title = album.AlbumTitle;
            Artist = album.ArtistName;
            Explicit = album.Explicit;
            TotalSize = _tracks.Sum(t => t.size);
        }
    }
}
