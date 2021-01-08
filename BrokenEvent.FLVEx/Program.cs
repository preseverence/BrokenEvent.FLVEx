using System;
using System.IO;

using BrokenEvent.LibFLVEx.FLV;
using BrokenEvent.Shared.Algorithms;

namespace BrokenEvent.FLVEx
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

      if (model.FromSeconds.HasValue && model.ToSeconds.HasValue && model.FromSeconds.Value >= model.ToSeconds.Value)
      {
        Console.WriteLine("Start of output window (from) should be larger than end (to).");
        return 1;
      }

      Console.WriteLine("Input file: {0}", model.InputFile);

      Stream inputStream;
      if (model.OutputFile == null)
      {
        Console.WriteLine("Loading whole file to memory.");
        inputStream = new MemoryStream();
        using (FileStream fs = new FileStream(model.InputFile, FileMode.Open, FileAccess.Read, FileShare.Read))
          fs.CopyTo(inputStream);
        inputStream.Position = 0;
      }
      else
        inputStream = new FileStream(model.InputFile, FileMode.Open, FileAccess.Read, FileShare.Read);

      DateTime fileDate = File.GetLastWriteTime(model.InputFile);

      FLVFile file = new FLVFile(inputStream);

      file.PrintReport();

      if (model.FromSeconds.HasValue)
        file.CutFromStart(TimeSpan.FromSeconds(model.FromSeconds.Value));
      if (model.ToSeconds.HasValue)
        file.CutToEnd(TimeSpan.FromSeconds(model.ToSeconds.Value));
      if (model.FilterPackets)
        file.FilterPackets();
      if (model.FixTimestamps)
        file.FixTimeStamps();
      if (model.FixMetadata)
        file.FixMetadata();
      if (model.RemoveMetadata)
        file.RemoveMetadata();


      if (!(model.FilterPackets || model.FixMetadata || model.FixTimestamps || model.RemoveMetadata || model.FromSeconds.HasValue || model.ToSeconds.HasValue))
      {
        Console.WriteLine("No actions set. Exiting.");
        return 0;
      }

      string outputFile = model.OutputFile ?? model.InputFile;
      Console.WriteLine("Writing: {0}", outputFile);
      file.Write(outputFile);

      inputStream.Dispose();

      if (model.PreserveDate)
        File.SetLastWriteTime(outputFile, fileDate);
      return 0;
    }
  }
}
