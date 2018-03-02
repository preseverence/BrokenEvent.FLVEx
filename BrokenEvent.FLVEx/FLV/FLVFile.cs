using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using BrokenEvent.FLVEx.FLV.Packets;
using BrokenEvent.Shared.Algorithms;

using ConsoleApp8.FLV;

namespace BrokenEvent.FLVEx.FLV
{
  public class FLVFile: IDisposable
  {
    public FLVHeader Header { get; }
    public List<BasePacket> Packets { get; } = new List<BasePacket>();
    public long Size { get; }
    public MetadataPacket Metadata { get; private set; }

    private Stream sourceStream;

    public FLVFile(string fileName): this(new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Write)) { }

    public FLVFile(Stream stream)
    {
      sourceStream = stream;
      Size = stream.Length;
      DataStream ds = new DataStream(stream);
      ds.BigEndian = true;
      ds.Unsecure = true;

      Header = new FLVHeader(ds);
      while (stream.Position < stream.Length)
      {
        BasePacket packet = PacketFactory.ReadPacket(ds);

        if (packet is MetadataPacket metadataPacket)
        {
          if (Metadata != null)
            throw new InvalidOperationException("Duplicate metadata not allowed");
          Metadata = metadataPacket;
        }

        Packets.Add(packet);
      }
    }

    public int VideoPacketsCount
    {
      get { return Packets.Count(e => e.PacketType == PacketType.VideoPayload); }
    }

    public int AudioPacketsCount
    {
      get { return Packets.Count(e => e.PacketType == PacketType.AudioPayload); }
    }

    public uint VideoDataBytes
    {
      get { return (uint)Packets.Where(e => e.PacketType == PacketType.VideoPayload).Sum(e => e.PayloadSize); }
    }

    public uint AudioDataBytes
    {
      get { return (uint)Packets.Where(e => e.PacketType == PacketType.AudioPayload).Sum(e => e.PayloadSize); }
    }

    public void FilterPackets()
    {
      int count = 0;
      int i = 0;
      while (i < Packets.Count)
      {
        if (Packets[i].PacketType != PacketType.AMFMetadata &&
            Packets[i].PacketType != PacketType.AudioPayload &&
            Packets[i].PacketType != PacketType.VideoPayload)
        {
          Packets.RemoveAt(i);
          count++;
        }
        else
          i++;
      }

      Console.WriteLine("Packets filtering done. Removed: {0} packets", count);
    }

    public void FixTimeStamps()
    {
      TimeSpan delta = TimeSpan.MaxValue;

      foreach (BasePacket packet in Packets)
        if (packet.TimeStamp.TotalSeconds > 0)
        {
          if (packet.TimeStamp < delta)
            delta = packet.TimeStamp;
        }

      Console.WriteLine("Found initial time delta: " + delta);

      foreach (BasePacket packet in Packets)
        if (packet.TimeStamp.TotalSeconds > 0)
          packet.TimeStamp -= delta;
    }

    public void FixMetadata()
    {
      if (Metadata == null)
      {
        Console.WriteLine("No metadata packet found. Creating new.");
        Metadata = new MetadataPacket(FLVHeader.HEADER_SIZE);
        Packets.Insert(0, Metadata);
      }

      // remove meaningless stuff
      Metadata.Variables.Remove("metadatacreator");
      Metadata.Variables.Remove("creationdate");
      Metadata.Variables.Remove("metadatadate");
      Metadata.Variables.Remove("datasize");
      Metadata.Variables.Remove("videodatarate");
      Metadata.Variables.Remove("audiodatarate");

      // lil` improvement
      Metadata.Variables["canSeekToEnd"] = true;

      // latest frame
      TimeSpan maxTimeStamp = Packets.Max(e => e.TimeStamp);

      // update seeking fields
      Metadata.Variables["duration"] = maxTimeStamp.TotalSeconds;
      Metadata.Variables["lasttimestamp"] = maxTimeStamp.TotalSeconds;
      Metadata.Variables["lastkeyframetimestamp"] = Packets
        .Where(e => e.PacketType == PacketType.VideoPayload && ((VideoPacket)e).FrameType == VideoFrameType.KeyFrame)
        .Max(e => e.TimeStamp).TotalSeconds;
      Console.WriteLine("  Video duration: {0} seconds", maxTimeStamp.TotalSeconds);

      // first audio/video packets
      VideoPacket videoPacket = (VideoPacket)Packets.First(e => e.PacketType == PacketType.VideoPayload);
      AudioPacket audioPacket = (AudioPacket)Packets.First(e => e.PacketType == PacketType.AudioPayload);

      // update audio data
      Metadata.Variables["audiosamplerate"] = audioPacket.GetSampleRate();
      Metadata.Variables["audiosamplesize"] = audioPacket.GetSoundSize();
      Metadata.Variables["stereo"] = audioPacket.GetStereo();
      Metadata.Variables["audiocodecid"] = audioPacket.GetSoundFormat();
      Metadata.Variables["audiodelay"] = videoPacket.TimeStamp.TotalSeconds;
      Metadata.Variables["audiosize"] = (double)AudioDataBytes;
      Console.WriteLine(
          "  Audio: {0} Hz {1} bits {2} Codec: {3} Delay {4} sec",
          audioPacket.GetSampleRate(),
          audioPacket.GetSoundSize(),
          audioPacket.GetStereo() ? "stereo" : "mono",
          audioPacket.SoundFormat,
          videoPacket.TimeStamp.TotalSeconds)
        ;

      // update video data
      Metadata.Variables["videosize"] = (double)VideoDataBytes;
      Metadata.Variables["videocodecid"] = videoPacket.GetCodecId();
      Console.WriteLine("  Video codec: {0}", videoPacket.CodecId);

      videoPacket = (VideoPacket)Packets.FirstOrDefault(e => e.PacketType == PacketType.VideoPayload && ((VideoPacket)e).Width > 0 && ((VideoPacket)e).Height > 0);
      if (videoPacket != null)
      {
        Metadata.Variables["width"] = (double)videoPacket.Width;
        Metadata.Variables["height"] = (double)videoPacket.Height;
        Console.WriteLine("  Video dimensions: {0}x{1}", videoPacket.Width, videoPacket.Height);
      }
    }

    public void RemoveMetadata()
    {
      if (Metadata == null)
        return;

      Packets.Remove(Metadata);
      Metadata = null;
    }

    public void Write(string filename)
    {
      using (FileStream fs = new FileStream(filename, FileMode.Create))
        Write(fs);
    }

    public void Write(Stream stream)
    {
      DataStream ds = new DataStream(stream);
      ds.BigEndian = true;

      Header.Write(ds);
      uint prevPacketSize = 0;
      foreach (BasePacket packet in Packets)
      {
        packet.PrevPacketSize = prevPacketSize;
        packet.Write(sourceStream, ds);
        prevPacketSize = packet.PayloadSize;
      }

      if (Metadata != null)
      {
        Metadata.Variables.Remove("filesize");
        Metadata.PostWrite(ds);
      }
    }

    public void Dispose()
    {
      sourceStream?.Dispose();
      sourceStream = null;
    }
  }
}
