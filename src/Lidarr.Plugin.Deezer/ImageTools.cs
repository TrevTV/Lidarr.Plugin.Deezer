using System;
using System.IO;
using SkiaSharp;

namespace NzbDrone.Plugin.Deezer
{
    public static class ImagesTools
    {
        public static byte[] Scale(byte[] imageBytes, int maxWidth, int maxHeight, SKEncodedImageFormat format = SKEncodedImageFormat.Jpeg, int quality = 100)
        {
            SKBitmap image = SKBitmap.Decode(imageBytes);

            var ratioX = (double)maxWidth / image.Width;
            var ratioY = (double)maxHeight / image.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(image.Width * ratio);
            var newHeight = (int)(image.Height * ratio);

            var info = new SKImageInfo(newWidth, newHeight);
            image = image.Resize(info, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

            using var ms = new MemoryStream();
            image.Encode(ms, format, quality);
            return ms.ToArray();
        }

        public static byte[] ReEncode(byte[] imageBytes, SKEncodedImageFormat format = SKEncodedImageFormat.Jpeg, int quality = 100)
        {
            SKBitmap image = SKBitmap.Decode(imageBytes);
            using var ms = new MemoryStream();
            image.Encode(ms, format, quality);
            return ms.ToArray();
        }
    }
}
