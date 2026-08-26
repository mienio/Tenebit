using Tenebit.Application.Protocols;

namespace Tenebit.Application.Abstractions;

public interface IProtocolPdfGenerator
{
    byte[] Render(ProtocolDocument document);
}
