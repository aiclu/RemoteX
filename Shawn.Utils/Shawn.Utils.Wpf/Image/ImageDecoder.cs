using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace Shawn.Utils.Wpf.Image
{
    public static class ImageDecoder
    {

        #region Bitmap
#if NETFRAMEWORK
        public static async Task<Bitmap?> GetBitmapAsync(string filePath, int decodePixelWidth = 0, int decodePixelHeight = 0)
        {
            var imageBytes = File.ReadAllBytes(filePath);
            if (imageBytes.Length == 0)
            {
                SimpleLogHelper.Warning($"{filePath} read 0 byte");
                return null;
            }
            return await GetBitmapAsync(imageBytes, decodePixelWidth, decodePixelHeight);
        }
#else
        public static async Task<Bitmap?> GetBitmapAsync(string filePath, int decodePixelWidth = 0, int decodePixelHeight = 0)
        {
            var imageBytes = await File.ReadAllBytesAsync(filePath);
            if (imageBytes.Length == 0)
            {
                SimpleLogHelper.Warning($"{filePath} read 0 byte");
                return null;
            }
            return await GetBitmapAsync(imageBytes, decodePixelWidth, decodePixelHeight);
        }
#endif

        public static async Task<Bitmap?> GetBitmapAsync(byte[] imageBytes, int decodePixelWidth = 0, int decodePixelHeight = 0)
        {
            var ret = await Task.Run(() => GetBitmap(imageBytes, decodePixelWidth, decodePixelHeight));
            return ret;
        }

        public static Bitmap? GetBitmap(string filePath, int decodePixelWidth = 0, int decodePixelHeight = 0)
        {
            SimpleLogHelper.Debug($"GetBitmap '{filePath}', decode({decodePixelWidth}, {decodePixelHeight})");
            var imageBytes = File.ReadAllBytes(filePath);
            return GetBitmap(imageBytes, decodePixelWidth, decodePixelHeight);
        }

        public static Bitmap? GetBitmap(byte[] imageBytes, int decodePixelWidth = 0, int decodePixelHeight = 0)
        {
            if (imageBytes?.Length > 0)
            {
                try
                {
                    var ms = new MemoryStream(imageBytes);
                    var bm = Bitmap.FromStream(ms).ToBitmap();
                    return bm;
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                }
            }
            return null;
        }


        #endregion


        #region BitmapSource
#if NETFRAMEWORK
        public static async Task<BitmapSource?> GetBitmapSourceAsync(string filePath, int decodePixelWidth = 0, int decodePixelHeight = 0)
        {
            var imageBytes = File.ReadAllBytes(filePath);
            if (imageBytes.Length == 0)
            {
                SimpleLogHelper.Warning($"{filePath} read 0 byte");
                return null;
            }
            return await GetBitmapSourceAsync(imageBytes, decodePixelWidth, decodePixelHeight);
        }
#else
        public static async Task<BitmapSource?> GetBitmapSourceAsync(string filePath, int decodePixelWidth = 0, int decodePixelHeight = 0)
        {
            if (File.Exists(filePath) == false)
                return null;
            var imageBytes = await File.ReadAllBytesAsync(filePath);
            if (imageBytes.Length == 0)
            {
                SimpleLogHelper.Warning($"{filePath} read 0 byte");
                return null;
            }
            return await GetBitmapSourceAsync(imageBytes, decodePixelWidth, decodePixelHeight);
        }
#endif

        public static async Task<BitmapSource?> GetBitmapSourceAsync(byte[] imageBytes, int decodePixelWidth = 0, int decodePixelHeight = 0)
        {
            var ret = await Task.Run(() => GetBitmapSource(imageBytes, decodePixelWidth, decodePixelHeight));
            return ret;
        }

        public static BitmapSource? GetBitmapSource(string filePath, int decodePixelWidth = 0, int decodePixelHeight = 0)
        {
            SimpleLogHelper.Debug($"GetBitmapSource '{filePath}', decode({decodePixelWidth}, {decodePixelHeight})");
            var imageBytes = File.ReadAllBytes(filePath);
            return GetBitmapSource(imageBytes, decodePixelWidth, decodePixelHeight);
        }

        public static BitmapSource? GetBitmapSource(byte[] imageBytes, int decodePixelWidth = 0, int decodePixelHeight = 0)
        {
            if (imageBytes?.Length > 0)
            {
                try
                {
                    var ms = new MemoryStream(imageBytes);
                    var bitmapImage = new System.Windows.Media.Imaging.BitmapImage();
                    bitmapImage.BeginInit();
                    bitmapImage.StreamSource = ms;
                    if (decodePixelWidth > 0)
                        bitmapImage.DecodePixelWidth = decodePixelWidth;
                    if (decodePixelHeight > 0)
                        bitmapImage.DecodePixelWidth = decodePixelHeight;
                    bitmapImage.EndInit();
                    bitmapImage.Freeze();
                    return bitmapImage;
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                }
            }
            return null;
        }


        #endregion



        #region Image


#if NETFRAMEWORK
        public static async Task<System.Drawing.Image?> GetImageAsync(string filePath, int decodePixelWidth = 0, int decodePixelHeight = 0)
        {
            var imageBytes = File.ReadAllBytes(filePath);
            if (imageBytes.Length == 0)
            {
                SimpleLogHelper.Warning($"{filePath} read 0 byte");
                return null;
            }
            return await GetImageAsync(filePath);
        }
#else
        public static async Task<System.Drawing.Image?> GetImageAsync(string filePath)
        {
            var imageBytes = await File.ReadAllBytesAsync(filePath);
            if (imageBytes.Length == 0)
            {
                SimpleLogHelper.Warning($"{filePath} read 0 byte");
                return null;
            }
            return await GetImageAsync(imageBytes);
        }
#endif
        public static async Task<System.Drawing.Image?> GetImageAsync(byte[] imageBytes)
        {
            var ret = await Task.Run(() => GetImage(imageBytes));
            return ret;
        }
        public static System.Drawing.Image? GetImage(string filePath)
        {
            var imageBytes = File.ReadAllBytes(filePath);
            return GetImage(imageBytes);
        }
        public static System.Drawing.Image? GetImage(byte[] imageBytes)
        {
            if (imageBytes?.Length > 0)
            {
                try
                {
                    var ms = new MemoryStream(imageBytes);
                    var img = System.Drawing.Image.FromStream(ms);
                    return img;
                }
                catch (Exception e)
                {
                    SimpleLogHelper.Error(e);
                }
            }
            return null;
        }




        #endregion




        public static async Task<BitmapMetadata?> GetImageMetaDataAsync(byte[] imageBytes)
        {
            var ret = await Task.Run(() => GetImageMetaData(imageBytes));
            return ret;
        }

        public static BitmapMetadata? GetImageMetaData(byte[] imageBytes)
        {
            var ms = new MemoryStream(imageBytes);
            var bitmapFrame = BitmapFrame.Create(ms, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.Default);
            var bitmapMetadata = bitmapFrame.Metadata as BitmapMetadata;
            return bitmapMetadata;
        }

        public static double GetOrientation(this BitmapMetadata bitmapMetadata)
        {
            const string orientationQuery = "System.Photo.Orientation";
            if ((bitmapMetadata != null) && (bitmapMetadata.ContainsQuery(orientationQuery)))
            {
                var o = bitmapMetadata.GetQuery(orientationQuery);
                //refer to http://www.impulseadventure.com/photo/exif-orientation.html for details on orientation values
                switch ((ushort?)o)
                {
                    case 6:
                        return 90D;
                    case 3:
                        return 180D;
                    case 8:
                        return 270D;
                }
            }
            return 0;
        }

        public enum ThumbnailSize
        {
            Small,
            Medium,
            Large,
            ExtraLarge
        }

        private static readonly object LockerGetThumbnailFromWinApi = new object();
        public static BitmapImage? GetThumbnailFromWinApi(string filePath, ThumbnailSize size = ThumbnailSize.Medium)
        {
            // 这里必须锁住，不然多线程同时读取缩略图会无错误崩溃，原因不明
            lock (LockerGetThumbnailFromWinApi)
            {
                if (File.Exists(filePath) == false)
                    return null;
                var shellFile = Microsoft.WindowsAPICodePack.Shell.ShellFile.FromFilePath(filePath);
                return size switch
                {
                    ThumbnailSize.Small => shellFile.Thumbnail.SmallBitmap.ToBitmapImage(),
                    ThumbnailSize.Medium => shellFile.Thumbnail.MediumBitmap.ToBitmapImage(),
                    ThumbnailSize.Large => shellFile.Thumbnail.LargeBitmap.ToBitmapImage(),
                    ThumbnailSize.ExtraLarge => shellFile.Thumbnail.ExtraLargeBitmap.ToBitmapImage(),
                    _ => throw new ArgumentOutOfRangeException(nameof(size), size, null)
                }; 
            }
        }

        public static async Task<BitmapImage?> GetThumbnailFromWinApiAsync(string filePath, ThumbnailSize size = ThumbnailSize.Medium)
        {
            var ret = await Task.Run(() => GetThumbnailFromWinApi(filePath, size));
            return ret;
        }





        #region ImageDimensions
        /// <summary>
        /// https://stackoverflow.com/questions/111345/getting-image-dimensions-without-reading-the-entire-file
        /// </summary>
        const string ErrorMessage = "Could not recognize image format.";
        private static readonly Dictionary<byte[], Func<BinaryReader, Size>> ImageFormatDecoders = new Dictionary<byte[], Func<BinaryReader, Size>>()
        {
            { new byte[]{ 0x42, 0x4D }, DecodeBitmap},
            { new byte[]{ 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, DecodeGif },
            { new byte[]{ 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, DecodeGif },
            { new byte[]{ 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, DecodePng },
            { new byte[]{ 0xff, 0xd8 }, DecodeJfif },
        };

        /// <summary>
        /// Gets the dimensions of an image.
        /// </summary>
        /// <param name="filePath">The filePath of the image to get the dimensions of.</param>
        /// <returns>The dimensions of the specified image.</returns>
        /// <exception cref="ArgumentException">The image was of an unrecognized format.</exception>
        public static Size GetDimensions(string filePath)
        {
            using var binaryReader = new BinaryReader(File.OpenRead(filePath));
            try
            {
                return GetDimensions(binaryReader);
            }
            catch
            {
                using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                using var image = System.Drawing.Image.FromStream(stream: file, useEmbeddedColorManagement: false, validateImageData: false);
                return new Size((int)image.PhysicalDimension.Width, (int)image.PhysicalDimension.Height);
            }
        }

        /// <summary>
        /// Gets the dimensions of an image.
        /// </summary>
        /// <exception cref="ArgumentException">The image was of an unrecognized format.</exception>
        public static Size GetDimensions(byte[] imageBytes)
        {
            var ms = new MemoryStream(imageBytes);
            using var binaryReader = new BinaryReader(ms);
            try
            {
                return GetDimensions(binaryReader);
            }
            catch
            {
                using var image = System.Drawing.Image.FromStream(stream: ms, useEmbeddedColorManagement: false, validateImageData: false);
                return new Size((int)image.PhysicalDimension.Width, (int)image.PhysicalDimension.Height);
            }
        }

        /// <summary>
        /// Gets the dimensions of an image.
        /// </summary>
        /// <exception cref="ArgumentException">The image was of an unrecognized format.</exception>    
        public static Size GetDimensions(BinaryReader binaryReader)
        {
            int maxMagicBytesLength = ImageFormatDecoders.Keys.OrderByDescending(x => x.Length).First().Length;

            byte[] magicBytes = new byte[maxMagicBytesLength];

            for (int i = 0; i < maxMagicBytesLength; i += 1)
            {
                magicBytes[i] = binaryReader.ReadByte();

                foreach (var kvPair in ImageFormatDecoders)
                {
                    if (magicBytes.StartsWith(kvPair.Key))
                    {
                        return kvPair.Value(binaryReader);
                    }
                }
            }

            throw new ArgumentException(ErrorMessage, "binaryReader");
        }

        private static bool StartsWith(this byte[] thisBytes, byte[] thatBytes)
        {
            for (int i = 0; i < thatBytes.Length; i += 1)
            {
                if (thisBytes[i] != thatBytes[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static short ReadLittleEndianInt16(this BinaryReader binaryReader)
        {
            byte[] bytes = new byte[sizeof(short)];
            for (int i = 0; i < sizeof(short); i += 1)
            {
                bytes[sizeof(short) - 1 - i] = binaryReader.ReadByte();
            }
            return BitConverter.ToInt16(bytes, 0);
        }

        private static int ReadLittleEndianInt32(this BinaryReader binaryReader)
        {
            byte[] bytes = new byte[sizeof(int)];
            for (int i = 0; i < sizeof(int); i += 1)
            {
                bytes[sizeof(int) - 1 - i] = binaryReader.ReadByte();
            }
            return BitConverter.ToInt32(bytes, 0);
        }

        private static Size DecodeBitmap(BinaryReader binaryReader)
        {
            binaryReader.ReadBytes(16);
            int width = binaryReader.ReadInt32();
            int height = binaryReader.ReadInt32();
            return new Size(width, height);
        }

        private static Size DecodeGif(BinaryReader binaryReader)
        {
            int width = binaryReader.ReadInt16();
            int height = binaryReader.ReadInt16();
            return new Size(width, height);
        }

        private static Size DecodePng(BinaryReader binaryReader)
        {
            binaryReader.ReadBytes(8);
            int width = binaryReader.ReadLittleEndianInt32();
            int height = binaryReader.ReadLittleEndianInt32();
            return new Size(width, height);
        }

        private static Size DecodeJfif(BinaryReader binaryReader)
        {
            while (binaryReader.ReadByte() == 0xff)
            {
                byte marker = binaryReader.ReadByte();
                short chunkLength = binaryReader.ReadLittleEndianInt16();

                if (marker == 0xc0)
                {
                    binaryReader.ReadByte();

                    int height = binaryReader.ReadLittleEndianInt16();
                    int width = binaryReader.ReadLittleEndianInt16();
                    return new Size(width, height);
                }

                binaryReader.ReadBytes(chunkLength - 2);
            }

            throw new ArgumentException(ErrorMessage);
        }
        #endregion
    }
}
