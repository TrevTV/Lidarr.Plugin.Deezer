using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.RemotePathMappings;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Download.Clients.Deezer
{
    public class Deezer : DownloadClientBase<DeezerSettings>
    {
        private readonly IDeezerProxy _proxy;

        public Deezer(IDeezerProxy proxy,
                      IConfigService configService,
                      IDiskProvider diskProvider,
                      IRemotePathMappingService remotePathMappingService,
                      Logger logger)
            : base(configService, diskProvider, remotePathMappingService, logger)
        {
            _proxy = proxy;
        }

        public override string Protocol => nameof(DeezerDownloadProtocol);

        public override string Name => "Deezer";

        public override IEnumerable<DownloadClientItem> GetItems()
        {
            var queue = _proxy.GetQueue(Settings);

            foreach (var item in queue)
            {
                item.DownloadClientInfo = DownloadClientItemClientInfo.FromDownloadClient(this, false);
                item.OutputPath = _remotePathMappingService.RemapRemoteToLocal(Settings.Host, item.OutputPath);
            }

            return queue;
        }

        public override void RemoveItem(DownloadClientItem item, bool deleteData)
        {
            if (deleteData)
            {
                DeleteItemData(item);
            }

            _proxy.RemoveFromQueue(item.DownloadId, Settings);
        }

        public override Task<string> Download(RemoteAlbum remoteAlbum, IIndexer indexer)
        {
            var release = remoteAlbum.Release;

            int bitrate;

            if (release.Codec == "FLAC")
            {
                bitrate = 9;
            }
            else if (release.Container == "320")
            {
                bitrate = 3;
            }
            else
            {
                bitrate = 1;
            }

            return Task.FromResult(_proxy.Download(release.DownloadUrl, bitrate, Settings));
        }

        public override DownloadClientInfo GetStatus()
        {
            var config = _proxy.GetSettings(Settings);

            return new DownloadClientInfo
            {
                IsLocalhost = Settings.Host == "127.0.0.1" || Settings.Host == "localhost",
                OutputRootFolders = new List<OsPath> { _remotePathMappingService.RemapRemoteToLocal(Settings.Host, new OsPath(config.DownloadLocation)) }
            };
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            failures.AddIfNotNull(TestSettings());
        }

        private ValidationFailure TestSettings()
        {
            var config = _proxy.GetSettings(Settings);

            if (!config.CreateAlbumFolder)
            {
                return new NzbDroneValidationFailure(string.Empty, "Deezer must have 'Create Album Folders' enabled")
                {
                    InfoLink = HttpRequestBuilder.BuildBaseUrl(Settings.UseSsl, Settings.Host, Settings.Port, Settings.UrlBase),
                    DetailedDescription = "Deezer must have 'Create Album Folders' enabled, otherwise Lidarr will not be able to import the downloads",
                };
            }

            if (!config.CreateSingleFolder)
            {
                return new NzbDroneValidationFailure(string.Empty, "Deezer must have 'Create folder structure for singles' enabled")
                {
                    InfoLink = HttpRequestBuilder.BuildBaseUrl(Settings.UseSsl, Settings.Host, Settings.Port, Settings.UrlBase),
                    DetailedDescription = "Deezer must have 'Create folder structure for singles' enabled, otherwise Lidarr will not be able to import single downloads",
                };
            }

            try
            {
                _proxy.Authenticate(Settings);
            }
            catch (DownloadClientException)
            {
                return new NzbDroneValidationFailure(string.Empty, "Could not login to Deezer. Invalid ARL?")
                {
                    InfoLink = HttpRequestBuilder.BuildBaseUrl(Settings.UseSsl, Settings.Host, Settings.Port, Settings.UrlBase),
                    DetailedDescription = "Deezer requires a valid ARL to initiate downloads",
                };
            }

            return null;
        }
    }
}
