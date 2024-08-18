using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DeezNET;
using DeezNET.Data;
using DeezNET.Exceptions;
using Newtonsoft.Json.Linq;
using NzbDrone.Plugin.Deezer;

namespace NzbDrone.Core.Download.Clients.Deezer.Queue
{
    // TODO: this entire class is a mess of jamming things together from another project
    //       it would be ideal to go through and clean it up
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
                        _id = Guid.NewGuid().ToString(),
                        _deezerUrl = deezerUrl,
                        _wantedBitrate = bitrate,
                        _status = DownloadItemStatus.Queued,
                        _title = await item.GetTitle().ConfigureAwait(true) ?? "Unknown",
                        _artist = await item.GetArtist().ConfigureAwait(true) ?? "Unknown",
                        _explicit = await item.IsExplicit().ConfigureAwait(true),
                        _trackCount = await item.GetTrackCount().ConfigureAwait(true)
                    };
                }
            }

            return item;
        }

        public string ID { get => _id; }
        public string Title { get => _title; }
        public string Artist { get => _artist; }
        public bool Explicit { get => _explicit; }

        public string DownloadFolder { get => _downloadFolder; }

        public DownloadItemStatus Status { get => _status; set => _status = value; }
        public Bitrate Bitrate { get => _wantedBitrate; }

        public float Progress { get => DownloadedTracks / TrackCount; }
        public int TrackCount { get => _trackCount; }
        public int DownloadedTracks { get => _downloadedTracks; }
        public int FailedTracks { get => _failedTracks; }

        private string _id;
        private string _title;
        private string _artist;
        private bool _explicit;
        private DownloadItemStatus _status;
        private int _trackCount;

        private int _downloadedTracks;
        private int _failedTracks;
        private List<(long, string)> _downloadedFiles = new();

        private string _downloadFolder = "";

        private DeezerURL _deezerUrl;
        private Bitrate _wantedBitrate;
        private long[] _tracks;

        private static DateTime _lastARLValidityCheck = DateTime.MinValue;

        private const string CDN_TEMPLATE = "https://e-cdn-images.dzcdn.net/images/cover/{0}/{1}x{1}-000000-80-0-0.jpg";
        private readonly byte[] FLAC_MAGIC = Encoding.ASCII.GetBytes("fLaC");

        public async Task DoDownload(DeezerSettings settings, CancellationToken cancellation = default)
        {
            _tracks ??= await _deezerUrl.GetAssociatedTracks(DeezerAPI.Instance.Client, token: cancellation);

            List<Task> tasks = new();
            using SemaphoreSlim semaphore = new(3, 3);
            foreach (var track in _tracks)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(cancellation).ConfigureAwait(true);

                    try
                    {
                        await DoTrackDownload(track, settings, cancellation).ConfigureAwait(true);
                        _downloadedTracks++;
                    }
                    catch (TaskCanceledException) { }
                    catch
                    {
                        _failedTracks++;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, cancellation));
            }

            await Task.WhenAll(tasks).ConfigureAwait(true);
            if (_failedTracks > 0)
                _status = DownloadItemStatus.Failed;
            else
                _status = DownloadItemStatus.Completed;
        }

        private async Task DoTrackDownload(long track, DeezerSettings settings, CancellationToken cancellation = default)
        {
            JToken page = await DeezerAPI.Instance.Client.GWApi.GetTrackPage(track, cancellation);

            JToken albumPage = await DeezerAPI.Instance.Client.GWApi.GetAlbumPage(page["DATA"]!["ALB_ID"]!.Value<long>(), cancellation);

            byte[] trackData = await DeezerAPI.Instance.Client.Downloader.GetRawTrackBytes(track,_wantedBitrate, null, cancellation);
            string extension = Enumerable.SequenceEqual(trackData[0..4], FLAC_MAGIC) ? "flac" : "mp3";

            trackData = await DeezerAPI.Instance.Client.Downloader.ApplyMetadataToTrackBytes(track, trackData, token: cancellation);

            string outPath = Path.Combine(settings.DownloadPath, MetadataUtilities.GetFilledTemplate("%albumartist%/%album%/", extension, page, albumPage));
            _downloadFolder = outPath;
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

            if (File.Exists(outPath))
                _downloadedFiles.Add((track, outPath));
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

        public async Task<string> GetCoverUrl(int resolution, CancellationToken cancellation = default)
        {
            return await _deezerUrl.GetCoverUrl(DeezerAPI.Instance.Client, resolution, cancellation);
        }

        public async Task<string> GetTitle(CancellationToken cancellation = default)
        {
            return await _deezerUrl.GetTitle(DeezerAPI.Instance.Client, cancellation);
        }

        public async Task<string> GetArtist(CancellationToken token = default)
        {
            long id = _deezerUrl.Id;
            switch (_deezerUrl.EntityType)
            {
                case EntityType.Track:
                    return (await DeezerAPI.Instance.Client.PublicApi.GetTrack(id, token))["artist"]["name"].ToString();
                case EntityType.Album:
                    return (await DeezerAPI.Instance.Client.PublicApi.GetAlbum(id, 0, -1, token))["artist"]["name"].ToString();
                case EntityType.ArtistTop:
                case EntityType.Artist:
                    return (await DeezerAPI.Instance.Client.PublicApi.GetArtist(id, token))["name"].ToString();
                case EntityType.Playlist:
                    return "Various Artists";
                default:
                    return null;
            }
        }

        public async Task<bool> IsExplicit(CancellationToken token = default)
        {
            long id = _deezerUrl.Id;
            switch (_deezerUrl.EntityType)
            {
                case EntityType.Track:
                    return (await DeezerAPI.Instance.Client.PublicApi.GetTrack(id, token))["explicit_lyrics"].Value<bool>();
                case EntityType.Album:
                    return (await DeezerAPI.Instance.Client.PublicApi.GetAlbum(id, 0, -1, token))["explicit_lyrics"].Value<bool>();
                case EntityType.ArtistTop:
                case EntityType.Artist:
                    return false;
                case EntityType.Playlist:
                    return false; // TODO: didn't feel like implementing this; i don't think it's used anyway
                default:
                    return false;
            }
        }

        public async Task<int> GetTrackCount(CancellationToken cancellation = default)
        {
            _tracks ??= await _deezerUrl.GetAssociatedTracks(DeezerAPI.Instance.Client, token: cancellation);
            return _tracks.Length;
        }
    }
}
