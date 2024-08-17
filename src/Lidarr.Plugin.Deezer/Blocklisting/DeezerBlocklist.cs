using System;
using System.Collections.Generic;
using System.Linq;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Download;
using NzbDrone.Core.Indexers;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Blocklisting
{
    public class DeezerBlocklist : IBlocklistForProtocol
    {
        private readonly IBlocklistRepository _blocklistRepository;

        public DeezerBlocklist(IBlocklistRepository blocklistRepository)
        {
            _blocklistRepository = blocklistRepository;
        }

        public string Protocol => nameof(DeezerDownloadProtocol);

        public bool IsBlocklisted(int artistId, ReleaseInfo release)
        {
            var blocklistedByTorrentInfohash = _blocklistRepository.BlocklistedByTorrentInfoHash(artistId, release.Guid);
            return blocklistedByTorrentInfohash.Any(b => SameRelease(b, release));
        }

        public Blocklist GetBlocklist(DownloadFailedEvent message)
        {
            return new Blocklist
            {
                ArtistId = message.ArtistId,
                AlbumIds = message.AlbumIds,
                SourceTitle = message.SourceTitle,
                Quality = message.Quality,
                Date = DateTime.UtcNow,
                PublishedDate = DateTime.Parse(message.Data.GetValueOrDefault("publishedDate")),
                Size = long.Parse(message.Data.GetValueOrDefault("size", "0")),
                Indexer = message.Data.GetValueOrDefault("indexer"),
                Protocol = message.Data.GetValueOrDefault("protocol"),
                Message = message.Message,
                TorrentInfoHash = message.Data.GetValueOrDefault("guid")
            };
        }

        private bool SameRelease(Blocklist item, ReleaseInfo release)
        {
            if (release.Guid.IsNotNullOrWhiteSpace())
            {
                return release.Guid.Equals(item.TorrentInfoHash);
            }

            return item.Indexer.Equals(release.Indexer, StringComparison.InvariantCultureIgnoreCase);
        }
    }
}
