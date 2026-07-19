using System.IO;
using System.Threading.Tasks;

namespace ERP.Domain.Interfaces;

public interface ITranscriptionService
{
    Task<string> TranscribeAsync(Stream audioStream, string fileName);
}
