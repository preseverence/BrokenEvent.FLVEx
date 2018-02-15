using System;
using System.IO;

using BrokenEvent.FLVMagic.FLV;
using BrokenEvent.Shared.Algorithms;

namespace BrokenEvent.FLVMagic
{
  class Program
  {
    static int Main(string[] args)
    {
      CmdModel model = new CmdModel();
      CommandLineParser<CmdModel> parser = new CommandLineParser<CmdModel>(model);
      parser.CaseSensitive = false;
      parser.AssignmentSyntax = true;
      parser.WriteUsageOnError = true;
      if (!parser.Parse(args))
        return 1;

      if (model.FixMetadata && model.RemoveMetadata)
      {
        Console.WriteLine("fixMeta and noMeta flags conflicts.");
        return 1;
      }

      Console.WriteLine("Input file: {0}", model.InputFile);

      Stream inputStream;
      if (model.OutputFile == null)
      {
        Console.WriteLine("Loading whole file to memory.");
        inputStream = new MemoryStream();
        using (FileStream fs = new FileStream(model.InputFile, FileMode.Open, FileAccess.Read, FileShare.Write))
          fs.CopyTo(inputStream);
        inputStream.Position = 0;
      }
      else
        inputStream = new FileStream(model.InputFile, FileMode.Open, FileAccess.Read, FileShare.Write);

      DateTime fileDate = File.GetLastWriteTime(model.InputFile);

      FLVFile file = new FLVFile(inputStream);

      Console.WriteLine("  Flags: {0}. Packets: {1}", file.Header.Flags, file.Packets.Count);
      Console.WriteLine("  Audio: {0} bytes ({1:P1})", file.AudioDataBytes, (float)file.AudioDataBytes / file.Size);
      Console.WriteLine("  Video: {0} bytes ({1:P1})", file.VideoDataBytes, (float)file.VideoDataBytes / file.Size);

      if (model.FilterPackets)
        file.FilterPackets();
      if (model.FixTimestamps)
        file.FixTimeStamps();
      if (model.FixMetadata)
        file.FixMetadata();
      if (model.RemoveMetadata)
        file.RemoveMetadata();

      if (!(model.FilterPackets | model.FixMetadata | model.FixTimestamps | model.RemoveMetadata))
      {
        Console.WriteLine("No actions set. Exiting.");
        return 0;
      }

      string outputFile = model.OutputFile ?? model.InputFile;
      Console.WriteLine("Writing: {0}" + outputFile);
      file.Write(outputFile);

      inputStream.Dispose();

      if (model.PreserveDate)
        File.SetLastWriteTime(outputFile, fileDate);
      return 0;
    }
  }
}
