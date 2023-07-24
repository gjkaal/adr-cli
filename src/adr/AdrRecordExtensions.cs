using Newtonsoft.Json;
using System.Diagnostics;
using System.IO.Abstractions;
using System.Runtime.InteropServices;
using System.Text;

namespace adr
{
    public static class AdrRecordExtensions
    {
        public static StringBuilder GetRecord(this AdrRecord record)
        {
            var settings = new JsonSerializerSettings { Formatting = Formatting.Indented };
            return GetRecord(record, settings);
        }

        public static StringBuilder GetRecord(this AdrRecord record, JsonSerializerSettings settings)
        {
            var data = JsonConvert.SerializeObject(record, settings);
            return new StringBuilder(data);
        }

        public static AdrRecord Launch(this AdrRecord record, IAdrSettings settings)
        {
            var fileInfo = settings.GetContentFile(record.FileName);
            if (!fileInfo.Exists)
            {
                throw new AdrException($"Could not locate {fileInfo.FullName}");
            }
            try
            {
                Process.Start(fileInfo.FullName);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var url = fileInfo.FullName.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", fileInfo.FullName);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", fileInfo.FullName);
                }
                else
                {
                    throw;
                }
            }
            return record;
        }

        public static AdrRecord Load(IFileSystem fs, string filePath)
        {
            try
            {
                var context = fs.File.ReadAllText(filePath);
                return JsonConvert.DeserializeObject<AdrRecord>(context);
            }
            catch
            {
                // TODO : Log error
            }
            return default;
        }

        public static AdrRecord PrepareForStorage(this AdrRecord record)
        {
            if (record == null) record = new AdrRecord();
            record.FileName = $"{record.RecordId:D5}-{SanitizeFileName(record.Title)}";
            return record;
        }

        public static void Validate(this AdrRecord record)
        {
            if (record.RecordId < 0) throw new AdrException("Record id must be a positive value");
            if (string.IsNullOrEmpty(record.Title)) throw new AdrException("Title cannot be empty");
        }
        private static string SanitizeFileName(string title)
        {
            return title
                .Replace(' ', '-')
                .ToLower();
        }
    }
}