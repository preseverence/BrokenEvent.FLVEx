using BrokenEvent.Shared.Algorithms;

namespace BrokenEvent.FLVEx.FLV.Packets
{
  class UnparsedPacket: StreamCopyPacket
  {
    internal UnparsedPacket(DataStream stream, uint prevPacketSize, PacketType type): base(stream, prevPacketSize, type)
    {
      SkipPayload(stream);
    }    
  }
}
