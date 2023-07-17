using Newtonsoft.Json;
using System.Text;
using System.Threading.Tasks;

namespace adr;
public interface IAdrRecordRepository
{
    JsonSerializerSettings SerializerSettings { get; }
    Task<StringBuilder> GetLayoutAsync(AdrRecord record);
    void WriteRecord(AdrRecord record);
}