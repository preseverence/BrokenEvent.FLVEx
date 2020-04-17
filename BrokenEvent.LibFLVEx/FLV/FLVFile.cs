using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using BrokenEvent.LibFLVEx.FLV.Packets;
using BrokenEvent.LibFLVEx.Shared;

namespace BrokenEvent.LibFLVEx.FLV
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

        if (packet.PacketType == 0 && packet.PayloadSize == 0)
        {
          if (stream.Position < stream.Length)
            Console.WriteLine("Zero data detected at {0}. Integrity is unrecoverable. Dropping the rest of stream ({1} bytes remains).", stream.Position, stream.Length - stream.Position);

          break;
        }

        if (packet is MetadataPacket metadataPacket)
        {
          if (Metadata != null)
          {
            Console.WriteLine("Discarding duplicate metadata packet at: {0}", metadataPacket.Offset);
            continue;
          }

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

    public TimeSpan Duration
    {
      get
      {
        TimeSpan start = Packets.Min(p => p.TimeStamp);
        TimeSpan end = Packets.Min(p => p.TimeStamp);
        return (end - start);
      }
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

      if (Verbose)
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

      if (Verbose)
        Console.WriteLine("Found initial time delta: " + delta);

      foreach (BasePacket packet in Packets)
        if (packet.TimeStamp.TotalSeconds > 0)
          packet.TimeStamp -= delta;
    }

    public void FixMetadata()
    {
      if (Metadata == null)
      {
        if (Verbose)
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

      // update last key frame, if possible
      BasePacket lastKeyFrame = Packets.LastOrDefault(e => e.PacketType == PacketType.VideoPayload && ((VideoPacket)e).FrameType == VideoFrameType.KeyFrame);

      if (lastKeyFrame != null)
        Metadata.Variables["lastkeyframetimestamp"] = lastKeyFrame.TimeStamp.TotalSeconds;

      if (Verbose)
        Console.WriteLine("  Video duration: {0} seconds", maxTimeStamp.TotalSeconds);

      // first audio/video packets
      VideoPacket videoPacket = (VideoPacket)Packets.First(e => e.PacketType == PacketType.VideoPayload);
      AudioPacket audioPacket = (AudioPacket)Packets.FirstOrDefault(e => e.PacketType == PacketType.AudioPayload);

      if (audioPacket == null)
      {
        Header.Flags &= ~FLVFlags.Audio;

        Metadata.Variables.Remove("audiosamplerate");
        Metadata.Variables.Remove("audiosamplesize");
        Metadata.Variables.Remove("stereo");
        Metadata.Variables.Remove("audiocodecid");
        Metadata.Variables.Remove("audiodelay");
        Metadata.Variables.Remove("audiosize");

        if (Verbose)
          Console.WriteLine("  Audio: no");
      }
      else
      { 
        // update audio data
        Metadata.Variables["audiosamplerate"] = audioPacket.GetSampleRate();
        Metadata.Variables["audiosamplesize"] = audioPacket.GetSoundSize();
        Metadata.Variables["stereo"] = audioPacket.GetStereo();
        Metadata.Variables["audiocodecid"] = audioPacket.GetSoundFormat();
        Metadata.Variables["audiodelay"] = videoPacket.TimeStamp.TotalSeconds;
        Metadata.Variables["audiosize"] = (double)AudioDataBytes;
        if (Verbose)
          Console.WriteLine(
              "  Audio: {0} Hz {1} bits {2} Codec: {3} Delay {4} sec",
              audioPacket.GetSampleRate(),
              audioPacket.GetSoundSize(),
              audioPacket.GetStereo() ? "stereo" : "mono",
              audioPacket.SoundFormat,
              videoPacket.TimeStamp.TotalSeconds
            );
      }

      // update video data
      Metadata.Variables["videosize"] = (double)VideoDataBytes;
      Metadata.Variables["videocodecid"] = videoPacket.GetCodecId();
      if (Verbose)
        Console.WriteLine("  Video codec: {0}", videoPacket.CodecId);

      videoPacket = (VideoPacket)Packets.FirstOrDefault(e => e.PacketType == PacketType.VideoPayload && ((VideoPacket)e).Width > 0 && ((VideoPacket)e).Height > 0);
      if (videoPacket != null)
      {
        Metadata.Variables["width"] = (double)videoPacket.Width;
        Metadata.Variables["height"] = (double)videoPacket.Height;
        if (Verbose)
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

      if (Metadata != null)
        Metadata.Variables["filesize"] = (double)0;

      foreach (BasePacket packet in Packets)
      {
        packet.PrevPacketSize = prevPacketSize;
        packet.Write(sourceStream, ds);
        prevPacketSize = packet.PayloadSize;
      }

      if (Metadata != null)
      {
        Metadata.Variables["filesize"] = (double)stream.Position;
        Metadata.PostWrite(ds);
      }
    }

    public bool Verbose { get; set; } = true;

    public void Dispose()
    {
      sourceStream?.Dispose();
      sourceStream = null;
    }

    public void CutFromStart(TimeSpan start)
    {
      if (Verbose)
        Console.WriteLine("Searching for keyframe nearest to {0}", (int)start.TotalSeconds);

      int keyframeIndex = -1;
      int headerIndex = -1;
      for (int j = 0; j < Packets.Count; j++)
      {
        BasePacket packet = Packets[j];
        if (packet.PacketType != PacketType.VideoPayload || ((VideoPacket)packet).FrameType != VideoFrameType.KeyFrame)
          continue;

        if (((VideoPacket)packet).IsHeader)
          headerIndex = j;

        if (packet.TimeStamp > start)
          break;
        keyframeIndex = j;
      }

      if (keyframeIndex == -1)
      {
        if (Verbose)
          Console.WriteLine("Keyframe not found.");
        return;
      }

      if (headerIndex == -1)
      {
        if (Verbose)
          Console.WriteLine("Header keyframe not found.");
        return;
      }

      if (Verbose)
        Console.WriteLine("Keyframe found at #{0}, header keyframe found at #{1}", keyframeIndex, headerIndex);

      int removed = 0;
      int i = 0;
      while (i < keyframeIndex)
      {
        BasePacket packet = Packets[i];

        if ((packet.PacketType == PacketType.VideoPayload ||
            packet.PacketType == PacketType.AudioPayload) && i != headerIndex)
        {
          // remove
          Packets.RemoveAt(i);
          // update removed counter
          removed++;
          // move end pointer
          keyframeIndex--;
        }
        else
          i++;
      }

      if (Verbose)
        Console.WriteLine("Removed {0} packets", removed);
    }

    public void CutToEnd(TimeSpan end)
    {
      if (Verbose)
        Console.WriteLine("Removing video and audio packets later than {0}", (int)end.TotalSeconds);

      int i = 0;
      int removed = 0;
      while (i < Packets.Count)
      {
        BasePacket packet = Packets[i];

        if ((packet.PacketType == PacketType.VideoPayload ||
            packet.PacketType == PacketType.AudioPayload) &&
            packet.TimeStamp > end)
        {
          Packets.RemoveAt(i);
          removed++;
        }
        else
          i++;
      }

      if (Verbose)
        Console.WriteLine("Removed {0} packets", removed);
    }

    public void PrintReport()
    {
      Console.WriteLine("  Flags: {0}. Packets: {1}", Header.Flags, Packets.Count);
      uint audioDataBytes = AudioDataBytes;
      uint videoDataBytes = VideoDataBytes;
      TimeSpan start = Packets.Min(p => p.TimeStamp);
      TimeSpan end = Packets.Max(p => p.TimeStamp);

      Console.WriteLine(" -- Audio: {0} bytes ({1:P1}) ({2} packets)", audioDataBytes, (float)audioDataBytes / Size, Packets.Count(p => p.PacketType == PacketType.AudioPayload));
      Console.WriteLine(" -- Video: {0} bytes ({1:P1}) ({2} packets)", videoDataBytes, (float)videoDataBytes / Size, Packets.Count(p => p.PacketType == PacketType.VideoPayload));
      Console.WriteLine(" -- Duration: {0} seconds (from {1} to {2})", (int)(end - start).TotalSeconds, (int)start.TotalSeconds, (int)end.TotalSeconds);
    }
  }
}
