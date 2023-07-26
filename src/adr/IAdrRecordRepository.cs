using System.IO.Abstractions;
using System.Text;
using System.Threading.Tasks;

namespace adr;
public interface IAdrRecordRepository
{
    Task<StringBuilder> GetLayoutAsync(AdrRecord record);
    Task<AdrRecord?> ReadMetadataAsync(int recordId);
    Task WriteRecordAsync(AdrRecord record);
}