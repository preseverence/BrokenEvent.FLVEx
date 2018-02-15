﻿using BrokenEvent.FLVEx.Utils;
using BrokenEvent.Shared.Algorithms;

namespace BrokenEvent.FLVEx.FLV.Packets
{
  class VideoPacket: StreamCopyPacket
  {
    public VideoFrameType FrameType { get; }
    public VideoCodecId CodecId { get; }
    public int Width { get; }
    public int Height { get; }

    public double GetCodecId()
    {
      return (double)CodecId;
    }

    internal VideoPacket(DataStream stream, uint prevPacketSize, PacketType type): base(stream, prevPacketSize, type)
    {
      byte b = stream.ReadByte();
      FrameType = (VideoFrameType)((b >> 4) & 0xF);
      CodecId = (VideoCodecId)((b >> 0) & 0xF);

      if (CodecId == VideoCodecId.AVC)
      {
        int w = 0, h = 0;
        if (AVC.Read(stream, ref w, ref h))
        {
          Width = w;
          Height = h;
        }
      }

      SkipPayload(stream);
    }

    public override string ToString()
    {
      return $"{base.ToString()} {FrameType}";
    }
  }

  enum VideoFrameType: byte
  {
    KeyFrame = 1,
    Interframe = 2,
    Disposable = 3,
    Generated = 4,
    InfoFrame = 5,
  }

  enum VideoCodecId: byte
  {
    JPEG = 1,
    SorensonH263,
    ScreenVideo,
    On2VP6,
    On2VP6Alpha,
    ScreenVideo2,
    AVC,
  }

  enum AVCPacketType
  {
    SequenceHeader,
    NALU,
    EndOfSequence
  }
}
