using System.Text;
using System.Threading.Tasks;

namespace adr;
public interface IAdrRecordRepository
{
    Task<StringBuilder> GetLayoutAsync(AdrRecord record);
    Task WriteRecordAsync(AdrRecord record);
}