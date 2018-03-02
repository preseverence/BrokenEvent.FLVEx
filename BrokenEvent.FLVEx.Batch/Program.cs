using System;
using System.IO;

using BrokenEvent.FLVEx.FLV;
using BrokenEvent.Shared.Algorithms;

namespace BrokenEvent.FLVEx.Batch
{
  class Program
  {
    public static string MakeRelativePath(string path, string rootPath)
    {
      if (!rootPath.EndsWith("\\"))
        rootPath += "\\";
      return Uri.UnescapeDataString(new Uri(rootPath).MakeRelativeUri(new Uri(path)).ToString()).Replace('/', '\\');
    }

    public static string MakeAbsolutePath(string path, string rootPath)
    {
      return Path.GetFullPath(Path.Combine(rootPath, path));
    }

    private static bool ProcessFile(string filepath, CmdModel model)
    {
      string relativePath = MakeRelativePath(filepath, model.InputDir);

      if (!(model.FilterPackets | model.FixMetadata | model.FixTimestamps | model.RemoveMetadata))
      {
        Console.WriteLine("Skipping: {0}", relativePath);
        return true;
      }

      Stream inputStream;
      if (model.OutputDir == null)
      {
        Console.WriteLine("Processing (MEM): {0}", relativePath);
        inputStream = new MemoryStream();
        using (FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Write))
          fs.CopyTo(inputStream);
        inputStream.Position = 0;
      }
      else
      {
        Console.WriteLine("Processing (DUB): {0}", relativePath);
        inputStream = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Write);
      }

      DateTime fileDate = File.GetLastWriteTime(filepath);

      string outputFile;
      try
      {
        using (FLVFile file = new FLVFile(inputStream))
        {
          file.Verbose = false;
          if (model.FilterPackets)
            file.FilterPackets();
          if (model.FixTimestamps)
            file.FixTimeStamps();
          if (model.FixMetadata)
            file.FixMetadata();
          if (model.RemoveMetadata)
            file.RemoveMetadata();

          if (model.OutputDir == null)
            outputFile = filepath;
          else
          {
            outputFile = MakeAbsolutePath(relativePath, model.OutputDir);
            Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
          }

          file.Write(outputFile);
        }
      }
      catch (Exception e)
      {
        Console.WriteLine(e.Message);
        return false;
      }

      if (model.PreserveDate)
        File.SetLastWriteTime(outputFile, fileDate);

      return true;
    }

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

      if (!Directory.Exists(model.InputDir))
      {
        Console.WriteLine("Directory not found: {0}", model.InputDir);
        return 1;
      }

      int count = 0;

      foreach (string file in Directory.EnumerateFiles(model.InputDir, "*" + model.ExtFilter, model.SearchOption))
      {
        if (!ProcessFile(file, model))
          return 2;
        count++;
      }

      Console.WriteLine("Done: {0} files.", count);
      return 0;
    }
  }
}
