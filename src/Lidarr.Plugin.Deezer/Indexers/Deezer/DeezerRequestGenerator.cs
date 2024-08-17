using System.Collections.Generic;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;

namespace NzbDrone.Core.Indexers.Deezer
{
    public class DeezerRequestGenerator : IIndexerRequestGenerator
    {
        private const int PageSize = 100;
        private const int MaxPages = 30;
        public DeezerIndexerSettings Settings { get; set; }
        public Logger Logger { get; set; }

        public virtual IndexerPageableRequestChain GetRecentRequests()
        {
            var pageableRequests = new IndexerPageableRequestChain();

            var url = $"{Settings.BaseUrl.TrimEnd('/')}/api/newReleases";

            pageableRequests.Add(new[]
            {
                new IndexerRequest(url, HttpAccept.Json)
            });

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(AlbumSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();

            chain.AddTier(GetRequests($"artist:\"{searchCriteria.ArtistQuery}\" album:\"{searchCriteria.AlbumQuery}\""));
            chain.AddTier(GetRequests($"{searchCriteria.ArtistQuery} {searchCriteria.AlbumQuery}"));

            return chain;
        }

        public IndexerPageableRequestChain GetSearchRequests(ArtistSearchCriteria searchCriteria)
        {
            var chain = new IndexerPageableRequestChain();

            chain.AddTier(GetRequests($"artist:\"{searchCriteria.ArtistQuery}\""));
            chain.AddTier(GetRequests(searchCriteria.ArtistQuery));

            return chain;
        }

        private IEnumerable<IndexerRequest> GetRequests(string searchParameters)
        {
            for (var page = 0; page < MaxPages; page++)
            {
                var url = $"{Settings.BaseUrl.TrimEnd('/')}/api/album-search?term={searchParameters}&nb={PageSize}&start={page * PageSize}";
                yield return new IndexerRequest(url, HttpAccept.Json);
            }
        }
    }
}
