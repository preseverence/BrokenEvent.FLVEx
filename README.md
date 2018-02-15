# BrokenEvent.FLVEx
FLV video file fixer/metadata injector.

## Features

* Packet timestamps fix. Tthis will fix length for semibroken video streams.
* Packet filtering. Will filter all packets, which aren't supported by videoplayers from videostream. Remain packets will be:
  * AMFMetadata
  * VideoPayload
  * AudioPayload
* Metadata injection.
* Metadata removing.

## Background
This code was born when I got tired of FLVs with broken length obtained from RTMP interception. Known metadata injectors can only inject timestamp of the last packet as video length. But it is not always the length, if the stream was captured not from the beginning. This code allows fix packets timestamps and get and inject true length of the video, regardless to the starting time.

## Usage:

```BrokenEvent.FLVEx.exe inFile [outFile] [-filter] [-fix] [-fixMeta] [-noMeta] [-preserve]```

#### Arguments:

```
inFile           Input FLV file.
outFile          Output filename. Will overwrite input file if omitted. Optional.
-filter, -f      Filters packets which are not required for video playback. Optional.
-fix             Fixes video timestamps. This will fix video duration for broken files. Optional.
-fixMeta, -meta  Fixes/adds metadata to the file. Optional.
-noMeta          Removes metadata from file. Not compatible with fixMeta. Optional.
-preserve        Preserved last file changes date of the file. Optional.
```
