using System.IO;

using BrokenEvent.Shared.Algorithms;

namespace BrokenEvent.FLVEx.Batch
{
  [CommandModel("BrokenEvent.FLVEx Batch Processor, (c) 2018-2020 BrokenEvent. All rights reserved.")]
  class CmdModel
  {
    [Command(0, "Input search directory.", "inDir", isRequired: true)]
    public string InputDir { get; set; }

    [Command("ext", "Files extension. Default is \".flv\"", ".flv")]
    public string ExtFilter { get; set; } = ".flv";

    [Command("recurse", "Enables subdirectories scan.", alias: "r",  isFlag: true)]
    public bool RecurseSubdirs { get; set; }

    [Command(1, "Output directory. Will overwrite input files if omitted. Will overwrite files in output directory without prompt.", "outDir")]
    public string OutputDir { get; set; }

    [Command("filter", "Filters packets which are not required for video playback, including broken packets.", alias: "f", isFlag: true)]
    public bool FilterPackets { get; set; }

    [Command("fixTime", "Fixes video timestamps. This will fix video duration for broken files.", alias: "fix", isFlag: true)]
    public bool FixTimestamps { get; set; }

    [Command("fixMeta", "Fixes/adds metadata to the file.", alias: "meta", isFlag: true)]
    public bool FixMetadata { get; set; }

    [Command("noMeta", "Removes metadata from file. Not compatible with fixMeta.", isFlag: true)]
    public bool RemoveMetadata { get; set; }

    [Command("preserve", "Preserves last file changes date of the file.", isFlag: true)]
    public bool PreserveDate { get; set; }

    [Command("parallel", "Enables parallelism. May lead to significant RAM, CPU and I/O usage. Will use 2 * Cores threads.", isFlag: true)]
    public bool Parallel { get; set; }

    [Command("wait", "Enables waiting for input on errors. May conflict with parallel processing.", isFlag: true)]
    public bool Wait { get; set; }

    [Command("ignore", "Ignore errors instead of exiting at once. Files with errors will be skipped.", isFlag: true)]
    public bool IgnoreErrors { get; set; }

    [Command("memCache", "Loads files into memory to reduce I/O operations. May consume significant amount of RAM", alias: "mem", isFlag: true)]
    public bool MemoryCache { get; set; }

    public SearchOption SearchOption
    {
      get { return RecurseSubdirs ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly; }
    }
  }
}
