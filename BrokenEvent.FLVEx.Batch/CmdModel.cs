using System.IO;

using BrokenEvent.Shared.Algorithms;

namespace BrokenEvent.FLVEx.Batch
{
  [CommandModel("BrokenEvent.FLVEx Batch Processor, (c) 2018 BrokenEvent. All rights reserved.")]
  class CmdModel
  {
    [Command(0, "Input search directory.", @"c:\video", isRequired: true)]
    public string InputDir { get; set; }

    [Command("ext", "Files extension. Default is \".flv\"", ".flv")]
    public string ExtFilter { get; set; } = ".flv";

    [Command("recurse", "Enables subdirectories scan.", isFlag: true)]
    public bool RecurseSubdirs { get; set; }

    [Command(1, "Output directory. Will overwrite input files if omitted.", @"c:\video_out")]
    public string OutputDir { get; set; }

    [Command("filter", "Filters packets which are not required for video playback.", alias: "f", isFlag: true)]
    public bool FilterPackets { get; set; }

    [Command("fix", "Fixes video timestamps. This will fix video duration for broken files.", isFlag: true)]
    public bool FixTimestamps { get; set; }

    [Command("fixMeta", "Fixes/adds metadata to the file.", alias: "meta", isFlag: true)]
    public bool FixMetadata { get; set; }

    [Command("noMeta", "Removes metadata from file. Not compatible with fixMeta.", isFlag: true)]
    public bool RemoveMetadata { get; set; }

    [Command("preserve", "Preserves last file changes date of the file.", isFlag: true)]
    public bool PreserveDate { get; set; }

    public SearchOption SearchOption
    {
      get { return RecurseSubdirs ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly; }
    }
  }
}
