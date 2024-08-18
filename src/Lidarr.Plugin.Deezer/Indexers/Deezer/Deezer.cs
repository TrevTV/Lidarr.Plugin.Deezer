using System;
using NLog;
using NzbDrone.Common.Cache;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Download.Clients.Deezer;
using NzbDrone.Core.Parser;
using NzbDrone.Plugin.Deezer;

namespace NzbDrone.Core.Indexers.Deezer
{
    public class Deezer : HttpIndexerBase<DeezerIndexerSettings>
    {
        public override string Name => "Deezer";
        public override string Protocol => nameof(DeezerDownloadProtocol);
        public override bool SupportsRss => true;
        public override bool SupportsSearch => true;
        public override int PageSize => 100;
        public override TimeSpan RateLimit => new TimeSpan(0);

        private readonly IDeezerProxy _deezerProxy;

        public Deezer(ICacheManager cacheManager,
            IDeezerProxy deezerProxy,
            IHttpClient httpClient,
            IIndexerStatusService indexerStatusService,
            IConfigService configService,
            IParsingService parsingService,
            Logger logger)
            : base(httpClient, indexerStatusService, configService, parsingService, logger)
        {
            _deezerProxy = deezerProxy;
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new DeezerRequestGenerator()
            {
                Settings = Settings,
                Logger = _logger
            };
        }

        public override IParseIndexerResponse GetParser()
        {
            return new DeezerParser();
        }
    }
}
