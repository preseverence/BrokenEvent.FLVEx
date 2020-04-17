using BrokenEvent.LibFLVEx.Shared;

namespace BrokenEvent.LibFLVEx.FLV.Packets
{
  public class UnparsedPacket: StreamCopyPacket
  {
    internal UnparsedPacket(DataStream stream, uint prevPacketSize, PacketType type): base(stream, prevPacketSize, type)
    {
      SkipPayload(stream);
    }    
  }
}
