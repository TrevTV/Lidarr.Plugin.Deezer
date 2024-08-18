using System;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace NzbDrone.Core.Download.Clients.Deezer
{
    public static class MetadataUtilities
    {
        public static string GetFilledTemplate(string template, string ext, JToken deezerPage, JToken deezerAlbumPage)
        {
            DateTime releaseDate = DateTime.Parse(deezerPage["DATA"]!["PHYSICAL_RELEASE_DATE"]!.ToString());
            return GetFilledTemplate_Internal(template,
                deezerPage["DATA"]!["SNG_TITLE"]!.ToString(),
                deezerPage["DATA"]!["ALB_TITLE"]!.ToString(),
                deezerAlbumPage["DATA"]!["ART_NAME"]!.ToString(),
                deezerPage["DATA"]!["ART_NAME"]!.ToString(),
                deezerAlbumPage["DATA"]!["ARTISTS"]!.Select(a => a["ART_NAME"]!.ToString()).ToArray(),
                deezerPage["DATA"]!["ARTISTS"]!.Select(a => a["ART_NAME"]!.ToString()).ToArray(),
                $"{(int)deezerPage["DATA"]!["TRACK_NUMBER"]!:00}",
                deezerAlbumPage["SONGS"]!["total"]!.ToString(),
                releaseDate.Year.ToString(),
                ext);
        }

        private static string GetFilledTemplate_Internal(string template, string title, string album, string albumArtist, string artist, string[] albumArtists, string[] artists, string track, string trackCount, string year, string ext)
        {
            StringBuilder t = new(template);
            ReplaceC("%title%", title);
            ReplaceC("%album%", album);
            ReplaceC("%albumartist%", albumArtist);
            ReplaceC("%artist%", artist);
            ReplaceC("%albumartists%", string.Join("; ", albumArtists));
            ReplaceC("%artists%", string.Join("; ", artists));
            ReplaceC("%track%", track);
            ReplaceC("%trackcount%", trackCount);
            ReplaceC("%ext%", ext);
            ReplaceC("%year%", year);

            return t.ToString();

            void ReplaceC(string o, string r)
            {
                t.Replace(o, CleanPath(r));
            }
        }

        public static string CleanPath(string str)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
            {
                char c = invalid[i];
                str = str.Replace(c, '_');
            }
            return str;
        }
    }
}
