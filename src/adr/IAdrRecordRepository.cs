using System.Text;
using System.Threading.Tasks;

namespace adr;
public interface IAdrRecordRepository
{
    Task<StringBuilder> GetLayoutAsync(AdrRecord record);
    Task<AdrRecord?> ReadMetadataAsync(int recordId);
    Task<string[]> ReadContentAsync(int recordId);
    Task WriteRecordAsync(AdrRecord record);
    Task<int> UpdateMetadataAsync(int recordId, AdrRecord record);
    Task<int> UpdateContentAsync(AdrRecord record, string[] lines);
}