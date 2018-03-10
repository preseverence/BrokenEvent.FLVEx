# BrokenEvent.FLVEx
FLV video file fixer/metadata injector.

## Features

* Packet timestamps fix. This will fix length for semibroken video streams.
* Packet filtering. Will filter all packets, which aren't supported by videoplayers from videostream. Remain packets will be:
  * AMFMetadata
  * VideoPayload
  * AudioPayload
* Metadata injection.
* Metadata removing.
* Incldues batch tool for multiple files processing.

## Background
This code was born when I got tired of FLVs with broken length obtained from RTMP interception. Known metadata injectors can only inject timestamp of the last packet as video length. But it is not always the length, if the stream was captured not from the beginning. This code allows fix packets timestamps and get and inject true length of the video, regardless to the starting time.

## FLVEx Usage:

```BrokenEvent.FLVEx.exe inFile [outFile] [-filter] [-fix] [-fixMeta] [-noMeta] [-preserve]```

#### Arguments:

```
inFile           Input FLV file.
outFile          Output filename. Will overwrite input file if omitted. Optional.
-filter, -f      Filters packets which are not required for video playback. Optional.
-fix             Fixes video timestamps. This will fix video duration for broken files. Optional.
-fixMeta, -meta  Fixes/adds metadata to the file. Optional.
-noMeta          Removes metadata from file. Not compatible with fixMeta. Optional.
-preserve        Preserves last file changes date of the file. Optional.
```

## FLVEx.Batch Usage:

```BrokenEvent.FLVEx.Batch.exe inDir [-ext .flv] [-recurse] [outDir] [-filter] [-fixTime] [-fixMeta] [-noMeta] [-preserve] [-parallel] [-wait] [-ignore] [-memCache]```

#### Arguments:

```
inDir           Input search directory.
-ext            Files extension. Default is ".flv" Optional.
-recurse, -r    Enables subdirectories scan. Optional.
outDir          Output directory. Will overwrite input files if omitted. Will overwrite files in output directory without prompt. Optional.
-filter, -f     Filters packets which are not required for video playback, including broken packets. Optional.
-fixTime, -fix  Fixes video timestamps. This will fix video duration for broken files. Optional.
-fixMeta, -meta Fixes/adds metadata to the file. Optional.
-noMeta         Removes metadata from file. Not compatible with fixMeta. Optional.
-preserve       Preserves last file changes date of the file. Optional.
-parallel       Enables parallelism. May lead to significant RAM, CPU and I/O us age. Will use 2 * Cores threads. Optional.
-wait           Enables waiting for input on errors. May conflict with parallel processing. Optional.
-ignore         Ignore errors instead of exiting at once. Files with errors will be skipped. Optional.
-memCache, -mem Loads files into memory to reduce I/O operations. May consume significant amount of RAM Optional.
