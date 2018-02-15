using System;
using System.Collections.Generic;
using System.Text;

using BrokenEvent.Shared.Algorithms;

namespace BrokenEvent.FLVMagic.FLV.Packets
{
  class MetadataPacket: RewritablePacket
  {
    public Dictionary<string, object> Variables { get; } = new Dictionary<string, object>();
    private const string MARKER = "onMetaData";

    private static string ReadASString(DataStream stream)
    {
      ushort length = stream.ReadUShort();
      if (length == 0)
        return null;

      byte[] buffer = stream.ReadBytes(length);
      return Encoding.ASCII.GetString(buffer);
    }

    private static void WriteASString(DataStream stream, string value)
    {
      byte[] data = Encoding.ASCII.GetBytes(value);
      stream.Write((ushort)data.Length);
      stream.Stream.Write(data, 0, data.Length);
    }

    internal static void ReadVars(DataStream stream, Dictionary<string, object> vars)
    {
      // this is not unknown stuff, it's just a string.
      stream.Position += 1; // skip some strange stuff

      if (ReadASString(stream) != MARKER)
        throw new InvalidOperationException("Invalid metadata marker");

      stream.Position += 1; // skip type mark
      int maxArrayIndex = stream.ReadInt();

      for (int i = 0; i < maxArrayIndex; i++)
      {
        string name = ReadASString(stream);

        KnownTypes type = (KnownTypes)stream.ReadByte();
        switch (type)
        {
          case KnownTypes.Double:
            vars.Add(name, stream.ReadDouble());
            break;

          case KnownTypes.Bool:
            vars.Add(name, stream.ReadBool());
            break;

          case KnownTypes.String:
            vars.Add(name, ReadASString(stream));
            break;

          case KnownTypes.Date:
            byte[] b = stream.ReadBytes(10);
            vars.Add(name, b);
            break;

          case KnownTypes.Null:
          case KnownTypes.Undefined:
          case KnownTypes.Unsupported:
            vars.Add(name, type);
            break;

          default:
            throw new InvalidOperationException("Invalid or unsupported data type: " + type);
        }
      }

      // skip "" and ObjectEnd marker
      stream.Position += 3;
    }

    internal static void WriteVars(DataStream stream, Dictionary<string, object> vars)
    {
      stream.Write((byte)KnownTypes.String);
      WriteASString(stream, MARKER);
      stream.Write((byte)KnownTypes.MixedArray);
      stream.Write(vars.Count); // count

      foreach (KeyValuePair<string, object> v in vars)
      {
        WriteASString(stream, v.Key);

        switch (v.Value) {
          case double d:
            stream.Write((byte)KnownTypes.Double);
            stream.Write(d);
            break;

          case bool b:
            stream.Write((byte)KnownTypes.Bool);
            stream.Write(b);
            break;

          case string s:
            stream.Write((byte)KnownTypes.String);
            WriteASString(stream, s);
            break;

          case byte[] ba:
            stream.Write((byte)KnownTypes.Date);
            stream.Stream.Write(ba, 0, ba.Length);
            break;

          case KnownTypes t:
            stream.Write((byte)t);
            break;
          default:
            throw new InvalidOperationException("Unsupported type: " + v.Value.GetType().Name);
        }
      }

      // write ""
      stream.Write((ushort)0);
      stream.Write((byte)KnownTypes.ObjectEnd);
    }

    internal MetadataPacket(long offset): base(PacketType.AMFMetadata, TimeSpan.Zero, offset)
    {
    }

    internal MetadataPacket(DataStream stream, uint prevPacketSize, PacketType type): base(stream, prevPacketSize, type)
    {
      ReadVars(stream, Variables);
    }

    protected override void WriteData(DataStream dest)
    {
      base.WriteData(dest);
      WriteVars(dest, Variables);
    }

    internal void PostWrite(DataStream dest)
    {
      dest.Position = Offset;
      WriteVars(dest, Variables);
    }

    public enum KnownTypes: byte
    {
      Double = 0,
      Bool = 1,
      String = 2,
      Object = 3,
      Null = 5,
      Undefined = 6,
      Reference = 7,
      MixedArray = 8,
      ObjectEnd = 9,
      Array = 10,
      Date = 11,
      LongString = 12,
      Unsupported = 13,
    }
  }
}
