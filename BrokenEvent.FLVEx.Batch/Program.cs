using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using BrokenEvent.FLVEx.FLV;
using BrokenEvent.Shared.Algorithms;

namespace BrokenEvent.FLVEx.Batch
{
  class Program
  {
    private static CmdModel model;
    private static int filesProcessed;
    private static int filesTotal;
    private static int cancelPending;

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

    private static bool ProcessFile(string filepath)
    {
      string relativePath = MakeRelativePath(filepath, model.InputDir);

      if (!(model.FilterPackets | model.FixMetadata | model.FixTimestamps | model.RemoveMetadata))
      {
        Console.WriteLine("Skip: {0} ({1}/{2})", relativePath, filesProcessed, filesTotal);
        return true;
      }

      Stream inputStream;
      if (model.OutputDir == null || model.MemoryCache)
      {
        Console.WriteLine("MEM: {0} ({1}/{2})", relativePath, filesProcessed, filesTotal);
        inputStream = new MemoryStream();
        using (FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read, FileShare.Write))
          fs.CopyTo(inputStream);
        inputStream.Position = 0;
      }
      else
      {
        Console.WriteLine("DUB: {0} ({1}/{2})", relativePath, filesProcessed, filesTotal);
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

      GC.Collect();

      return true;
    }

    static int Main(string[] args)
    {
      model = new CmdModel();
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

      Console.TreatControlCAsInput = true;
      Console.CancelKeyPress += OnCancel;
     
      Stopwatch sw = Stopwatch.StartNew();
      List<string> files = new List<string>(Directory.EnumerateFiles(model.InputDir, "*" + model.ExtFilter, model.SearchOption));
      filesTotal = files.Count;
      Console.WriteLine("Found {0} files.", files.Count);

      if (model.Parallel)
      {
        ParallelOptions options = new ParallelOptions();
        options.MaxDegreeOfParallelism = Environment.ProcessorCount * 2;
        ParallelLoopResult result = Parallel.ForEach(files, options, DoParallel);
        if (!result.IsCompleted)
        {
          if (model.Wait)
            Console.ReadLine();
          return 2;
        }
      }
      else
        foreach (string file in files)
        {
          Interlocked.Add(ref filesProcessed, 1);
          if (!ProcessFile(file) && !model.IgnoreErrors)
          {
            if (model.Wait)
              Console.ReadLine();
            return 2;
          }

          if (cancelPending > 0)
            break;
        }

      Console.WriteLine("Done in {0} seconds.", (long)sw.Elapsed.TotalSeconds);

      if (model.Wait)
        Console.ReadLine();

      return 0;
    }

    private static void OnCancel(object s, ConsoleCancelEventArgs a)
    {
      Console.WriteLine("Cancel requested. Batch will stop soon.");
      Interlocked.Exchange(ref cancelPending, 1);
    }

    private static void DoParallel(string s, ParallelLoopState state)
    {
      Interlocked.Add(ref filesProcessed, 1);
      if (!ProcessFile(s) && !model.IgnoreErrors)
        state.Break();

      if (cancelPending > 0)
        state.Break();
    }
  }
}
