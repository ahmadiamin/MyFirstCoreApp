using MyFirstCoreApp.Services.Interfaces;

namespace MyFirstCoreApp.Services
{
    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        private static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".jpg",
                ".jpeg",
                ".png",
                ".webp"
            };

        private static readonly HashSet<string> AllowedContentTypes =
            new(StringComparer.OrdinalIgnoreCase)
            {
                "image/jpeg",
                "image/png",
                "image/webp"
            };

        public FileService(
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            _environment = environment;
            _configuration = configuration;
        }

        public async Task<string> SaveAsync(
            IFormFile file,
            string folder)
        {
            ValidateFile(file);

            var maxSizeInMB =
                _configuration.GetValue<int>(
                    "FileStorage:Images:MaxSizeInMB");

            var maxSize =
                maxSizeInMB * 1024L * 1024L;

            if (file.Length > maxSize)
            {
                throw new InvalidOperationException(
                    $"حجم فایل نباید بیشتر از {maxSizeInMB} مگابایت باشد.");
            }

            var maxWidth =
                _configuration.GetValue<int>(
                    "FileStorage:Images:MaxWidth");

            var maxHeight =
                _configuration.GetValue<int>(
                    "FileStorage:Images:MaxHeight");

            await ValidateImageAsync(
                file,
                maxWidth,
                maxHeight);

            var rootPath =
                _configuration["FileStorage:RootPath"]
                ?? "Archieve";

            var folderPath = Path.Combine(
                _environment.ContentRootPath,
                rootPath,
                folder);

            Directory.CreateDirectory(folderPath);

            var extension =
                GetSafeExtension(file);

            var fileName =
                $"{Guid.NewGuid():N}{extension}";

            var filePath = Path.Combine(
                folderPath,
                fileName);

            await using var stream =
                new FileStream(
                    filePath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None);

            await file.CopyToAsync(stream);

            return Path.Combine(
                    folder,
                    fileName)
                .Replace("\\", "/");
        }

        private static void ValidateFile(
            IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new InvalidOperationException(
                    "فایلی انتخاب نشده است.");
            }

            var extension =
                Path.GetExtension(file.FileName);

            if (!AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException(
                    "فرمت فایل مجاز نیست. فقط JPG، PNG و WebP مجاز هستند.");
            }

            if (!AllowedContentTypes.Contains(file.ContentType))
            {
                throw new InvalidOperationException(
                    "نوع فایل معتبر نیست.");
            }
        }

        private static async Task ValidateImageAsync(
            IFormFile file,
            int maxWidth,
            int maxHeight)
        {
            await using var stream =
                file.OpenReadStream();

            var header = new byte[32];

            var bytesRead =
                await stream.ReadAsync(header);

            var result =
                DetectImage(header, bytesRead, stream);

            if (result == null)
            {
                throw new InvalidOperationException(
                    "فایل انتخاب شده یک تصویر معتبر نیست.");
            }

            if (!result.Value.ContentType.Equals(
                    file.ContentType,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "نوع واقعی تصویر با نوع فایل اعلام شده مطابقت ندارد.");
            }

            if (result.Value.Width > maxWidth ||
                result.Value.Height > maxHeight)
            {
                throw new InvalidOperationException(
                    $"ابعاد تصویر نباید بیشتر از {maxWidth}×{maxHeight} پیکسل باشد.");
            }
        }

        private static ImageInfo? DetectImage(
            byte[] header,
            int bytesRead,
            Stream stream)
        {
            // PNG
            if (bytesRead >= 24 &&
                header[0] == 0x89 &&
                header[1] == 0x50 &&
                header[2] == 0x4E &&
                header[3] == 0x47 &&
                header[4] == 0x0D &&
                header[5] == 0x0A &&
                header[6] == 0x1A &&
                header[7] == 0x0A)
            {
                var width =
                    checked((int)ReadUInt32BigEndian(header, 16));

                var height =
                    checked((int)ReadUInt32BigEndian(header, 20));

                return new ImageInfo(
                    "image/png",
                    width,
                    height);
            }

            // WebP
            if (bytesRead >= 30 &&
                header[0] == 'R' &&
                header[1] == 'I' &&
                header[2] == 'F' &&
                header[3] == 'F' &&
                header[8] == 'W' &&
                header[9] == 'E' &&
                header[10] == 'B' &&
                header[11] == 'P')
            {
                stream.Position = 0;

                return DetectWebP(stream);
            }

            // JPEG
            if (bytesRead >= 2 &&
                header[0] == 0xFF &&
                header[1] == 0xD8)
            {
                stream.Position = 2;

                return DetectJpeg(stream);
            }

            return null;
        }

        private static ImageInfo? DetectJpeg(
            Stream stream)
        {
            while (stream.Position < stream.Length)
            {
                int markerStart = stream.ReadByte();

                if (markerStart != 0xFF)
                    continue;

                int marker;

                do
                {
                    marker = stream.ReadByte();
                }
                while (marker == 0xFF);

                if (marker == -1)
                    break;

                // Start of Scan / End of Image
                if (marker == 0xDA ||
                    marker == 0xD9)
                {
                    break;
                }

                // Standalone markers
                if (marker >= 0xD0 &&
                    marker <= 0xD7)
                {
                    continue;
                }

                var length = ReadUInt16BigEndian(stream);

                if (length < 2)
                    return null;

                // SOF markers
                if (IsStartOfFrame(marker))
                {
                    stream.ReadByte();

                    var height =
                        ReadUInt16BigEndian(stream);

                    var width =
                        ReadUInt16BigEndian(stream);

                    if (width <= 0 || height <= 0)
                        return null;

                    return new ImageInfo(
                        "image/jpeg",
                        width,
                        height);
                }

                stream.Seek(
                    length - 2,
                    SeekOrigin.Current);
            }

            return null;
        }

        private static ImageInfo? DetectWebP(
            Stream stream)
        {
            var header = new byte[30];

            var bytesRead =
                stream.Read(header, 0, header.Length);

            if (bytesRead < 16)
                return null;

            var chunkType =
                System.Text.Encoding.ASCII.GetString(
                    header,
                    12,
                    4);

            // VP8
            if (chunkType == "VP8 ")
            {
                return DetectWebPVp8(header);
            }

            // VP8L
            if (chunkType == "VP8L")
            {
                return DetectWebPVp8L(header);
            }

            // VP8X
            if (chunkType == "VP8X")
            {
                return DetectWebPVp8X(header);
            }

            return null;
        }

        private static ImageInfo? DetectWebPVp8(
            byte[] header)
        {
            if (header.Length < 30)
                return null;

            // VP8 frame header starts at byte 20
            if (header[23] != 0x9D ||
                header[24] != 0x01 ||
                header[25] != 0x2A)
            {
                return null;
            }

            var width =
                BitConverter.ToUInt16(header, 26)
                & 0x3FFF;

            var height =
                BitConverter.ToUInt16(header, 28)
                & 0x3FFF;

            if (width <= 0 || height <= 0)
                return null;

            return new ImageInfo(
                "image/webp",
                width,
                height);
        }

        private static ImageInfo? DetectWebPVp8L(
            byte[] header)
        {
            if (header.Length < 25)
                return null;

            byte b0 = header[21];
            byte b1 = header[22];
            byte b2 = header[23];
            byte b3 = header[24];

            int width =
                1 +
                ((b1 & 0x3F) << 8) |
                b0;

            int height =
                1 +
                ((b3 & 0xF) << 10) |
                (b2 << 2) |
                ((b1 & 0xC0) >> 6);

            if (width <= 0 || height <= 0)
                return null;

            return new ImageInfo(
                "image/webp",
                width,
                height);
        }

        private static ImageInfo? DetectWebPVp8X(
            byte[] header)
        {
            if (header.Length < 30)
                return null;

            int width =
                1 +
                header[24] |
                (header[25] << 8) |
                (header[26] << 16);

            int height =
                1 +
                header[27] |
                (header[28] << 8) |
                (header[29] << 16);

            if (width <= 0 || height <= 0)
                return null;

            return new ImageInfo(
                "image/webp",
                width,
                height);
        }

        private static bool IsStartOfFrame(
            int marker)
        {
            return marker switch
            {
                0xC0 or
                0xC1 or
                0xC2 or
                0xC3 or
                0xC5 or
                0xC6 or
                0xC7 or
                0xC9 or
                0xCA or
                0xCB or
                0xCD or
                0xCE or
                0xCF => true,

                _ => false
            };
        }

        private static ushort ReadUInt16BigEndian(
            Stream stream)
        {
            int high = stream.ReadByte();
            int low = stream.ReadByte();

            if (high < 0 || low < 0)
                throw new EndOfStreamException();

            return (ushort)((high << 8) | low);
        }

        private static uint ReadUInt32BigEndian(
            byte[] buffer,
            int offset)
        {
            return
                ((uint)buffer[offset] << 24) |
                ((uint)buffer[offset + 1] << 16) |
                ((uint)buffer[offset + 2] << 8) |
                buffer[offset + 3];
        }

        private static string GetSafeExtension(
            IFormFile file)
        {
            return file.ContentType.ToLowerInvariant() switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/webp" => ".webp",

                _ => throw new InvalidOperationException(
                    "فرمت تصویر مجاز نیست.")
            };
        }

        public Task DeleteAsync(
            string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return Task.CompletedTask;

            var physicalPath =
                GetPhysicalPath(relativePath);

            if (File.Exists(physicalPath))
            {
                File.Delete(physicalPath);
            }

            return Task.CompletedTask;
        }

        public string GetPhysicalPath(
            string relativePath)
        {
            var rootPath =
                _configuration["FileStorage:RootPath"]
                ?? "Archieve";

            return Path.Combine(
                _environment.ContentRootPath,
                rootPath,
                relativePath.Replace(
                    "/",
                    Path.DirectorySeparatorChar.ToString()));
        }

        private readonly record struct ImageInfo(
            string ContentType,
            int Width,
            int Height);

        public string? GetExistingPhysicalPath(string relativePath)
        {
            var physicalPath = GetPhysicalPath(relativePath);

            if (!File.Exists(physicalPath))
                return null;

            return physicalPath;
        }
    }
}