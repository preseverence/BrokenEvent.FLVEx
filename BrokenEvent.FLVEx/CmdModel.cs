using BrokenEvent.Shared.Algorithms;

namespace BrokenEvent.FLVMagic
{
  [CommandModel("BrokenEvent.FLVEx, (c) 2018 BrokenEvent. All rights reserved.")]
  class CmdModel
  {
    [Command(0, "Input FLV file", "inFile", isRequired: true)]
    public string InputFile { get; set; }

    [Command(1, "Output filename. Will overwrite input file if omitted.", "outFile")]
    public string OutputFile { get; set; }

    [Command("filter", "Filters packets which are not required for video playback.", alias: "f", isFlag: true)]
    public bool FilterPackets { get; set; }

    [Command("fix", "Fixes video timestamps. This will fix video duration for broken files.", isFlag: true)]
    public bool FixTimestamps { get; set; }

    [Command("fixMeta", "Fixes/adds metadata to the file.", alias: "meta", isFlag: true)]
    public bool FixMetadata { get; set; }

    [Command("noMeta", "Removes metadata from file. Not compatible with fixMeta.", isFlag: true)]
    public bool RemoveMetadata { get; set; }

    [Command("preserve", "Preserved last file changes date of the file", isFlag: true)]
    public bool PreserveDate { get; set; }
  }
}
