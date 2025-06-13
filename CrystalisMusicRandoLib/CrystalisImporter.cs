using FtRandoLib.Library;
using FtRandoLib.Importer;
using FtRandoLib.Utility;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Text;
using System.Diagnostics;

namespace CrystalisMusicRandoLib;

internal class CrystalisImporter : Importer
{
    class AreaTracksEntry
    {
        public string Name { get; }
        public string Usage { get; }
        public IReadOnlyList<int> TrackIndices { get; }

        public AreaTracksEntry(string areaName, string usage, IReadOnlyList<int> trackIndices)
        {
            Name = areaName;
            Usage = usage;
            TrackIndices = trackIndices;
        }

        public AreaTracksEntry(string areaName, string usage, IEnumerable<IReadOnlyList<int>> trackRanges)
        {
            List<int> idcs = new();

            Name = areaName;
            Usage = usage;
            TrackIndices = idcs;

            foreach (var idxRng in trackRanges)
            {
                switch (idxRng.Count)
                {
                    case 1:
                        idcs.Add(idxRng[0]); 
                        break;

                    case 2:
                        idcs.AddRange(Enumerable.Range(idxRng[0], idxRng[1] - idxRng[0]));
                        break;

                    default:
                        idcs.AddRange(idxRng);
                        break;
                }
            }
        }
    }

    static int _bankSize = 0x2000;
    protected override int BankSize => _bankSize;
    protected override List<int> FreeBanks { get; }

    protected override int PrimarySquareChan => 0;
    protected override IstringSet Uses { get; } = new(UsesSongIndices.Keys);
    protected override IstringSet DefaultUses { get; } = ["Dungeon"];
    protected override bool DefaultStreamingSafe => true;

    protected override int SongMapOffs => 0x32020 + 0x10;
    protected override int SongModAddrTblOffs => 0x320c0 + 0x10;

    const int _numSongs = 0x4f;
    const int NumBuiltinSongs = 0x20;

    static Range[] _builtInSongIdxRngs = [new(0, 2), new(3, 5), new(6, 0x10), new(0x11, NumBuiltinSongs)];
    protected override HashSet<int> BuiltinSongIdcs { get; } = _builtInSongIdxRngs.SelectMany(r => Enumerable.Range(r.Start.Value, r.End.Value - r.Start.Value)).ToHashSet();
    protected override List<int> FreeSongIdcs => Enumerable.Concat(
        [2, 5, 0x10],
        Enumerable.Range(NumBuiltinSongs, _numSongs - NumBuiltinSongs)).ToList();
    protected override int NumSongs => _numSongs;

    protected override IReadOnlyDictionary<string, SongMapInfo> SongMapInfos { get; } = new Dictionary<string, SongMapInfo>();

    protected override int NumFtChannels => 5;
    protected override int DefaultFtStartAddr => 0;
    protected override int DefaultFtPrimarySquareChan => 0;

    static Dictionary<string, BankLayout> _bankLayouts = new()
    {
        { "ft", new(0xa000, _bankSize) },
    };

    static string?[] _builtinSongNames =
    [
        "Silence",
        "Fields",
        null,
        "Poison Swamp",
        "Desert",
        null,
        "Ocean Waves",
        "Underground Rivers",
        "Fortress",
        "One Hope",
        "The End Day",
        "Title Screen",
        "Emperor Draygon",
        "Shyron",
        "Fortune Telling",
        "Portoa Royalty",
        null,
        "Earth Cave",
        "Boss Battle",
        "Power Increase",
        "Final Confrontation",
        "Tower in the Sky",
        "Town",
        "Ice Cave",
        "Inner Mountains",
        "Pyramids",
        "Ending Part 3",
        "Mesia",
        "Ending Part 2",
        "Ending Part 1",
        "Ominous Introduction",
        "Activation",
        "Town 2",
        "Town 3",
        "Town 4",
        "Town 5",
        null, // For some reason the randomizer uses this to stop music for stats
        "Boss 2",
        "Boss 3",
    ];
    static Dictionary<string, int[]> UsesSongIndices = new()
    {
        { "Overworld", [1, 4] },
        { "Dungeon", [3, 7, 8, 0x11, 0x15, 0x17, 0x18, 0x19] },
        { "Sea", [6] },
        { "Draygon", [0xc] },
        { "Mesia", [0x1b] },
        { "Town", [/*0xd,*/ 0x16, 0x20, 0x21, 0x22, 0x23] },
        { "Boss", [0x14, 0x12, 0x25, 0x26] },
        { "Credits", [0x1c] },
    };

    static readonly IstringDictionary<int> NumUsesSongs = new(UsesSongIndices.ToDictionary(kv => kv.Key, kv => kv.Value.Length));

    const int AreaTable16kBankIndex = 5;
    const int AreaTableAddress = 0x8300;

    const int AreaMusicTableOffset = 0x32160 + 0x10;
    const int AreaTableLength = 0x100;

    static readonly AreaTracksEntry[] RegionInfos = [
        new("Leaf", "Town", [[1, 3], [0xc0, 0xc6]]),
        new("Valley of Wind", "Dungeon", [[4, 0xb], [0xc], [0xe]]),
        new("Mt Sabre West", "Dungeon", [[0x11], [0x20, 0x28]]),
        new("Brynmaer", "Town", [[0x18], [0xc6, 0xcc]]),
        new("Mt Sabre North", "Dungeon", [[0x28, 0x35], [0x38], [0x39]]),
        new("Swamp", "Dungeon", [0x1a]),
        new("Amazones", "Town", [[0x1b], [0xd1, 0xd5], [0xe2]]),
        new("Oak", "Town", [[0x1c], [0xcd, 0xd1]]),
        new("Nadare", "Town", [[0x3c, 0x3f], [0xd5]]),
        new("Kirisa Plant Cave", "Dungeon", [[0x44, 0x47]]),
        new("Fog Lamp Cave", "Dungeon", [[0x48, 0x50]]),
        new("Portoa", "Town", [[0x50, 0x51, 0xd6, 0xd7], [0xd9, 0xdf], [0xe0]]),
        new("Boat House", "Sea", [0x61]), // Change from town to sea
        new("Zombie", "Town", [0x65, 0xe8, 0xe9]), //  First index is normally overworld
        new("Evil Spirit Island", "Dungeon", [[0x68, 0x6c]]),
        new("Sabera Palace", "Dungeon", [[0x6c, 0x70]]),
        new("Joel", "Town", [[0x71], [0x70], [0xe3, 0xe8]]),
        new("Swan", "Town", [[0x72, 0x74], [0xeb, 0xf2]]),
        new("Mt Hydra", "Dungeon", [[0x7c, 0x89]]),
        new("Styx", "Dungeon", [[0x88, 0x8b]]),
        // Do NOT randomize Shyron so players can use the music as a hint for which shops belong to Shyron when connections are randomized
        //new("Shyron", "Town", [[0x8c], [0xf2, 0xf8]]),
        new("Goa", "Town", [[0x8e], [0xbb, 0xc0]]), // First index is normally overworld
        new("Desert", "Overworld", [[0x90], [0x92], [0x95, 0x99]]),
        new("Goa Fortress 1&2", "Dungeon", [[0xa8, 0xad]]),
        new("Goa Fortress 3&4", "Dungeon", [[0xad, 0xbb]]),
        new("Oasis", "Dungeon", [0x8f, 0x91, 0xb8]),
        new("Sahara", "Town", [[0x93, 0x95], [0xf8, 0xfc]]),
        new("Pyramid", "Dungeon", [[0x9c, 0xa0]]),
        new("Crypt", "Dungeon", [[0xa0, 0xa7]]),
        new("Tower", "Dungeon", [[0x58, 0x5e]]),
    ];

    const int BossMusicTableOffset = 0x32260 + 0x10;
    const int BossMusicTableLength = 7;

    public CrystalisImporter(byte[] rom, IReadOnlyList<int> freeBanks)
        : base(_bankLayouts, new SimpleRomAccess(rom))
    {
        FreeBanks = freeBanks.ToList();

        //PrintAreaSongIdcs();
    }

    public IEnumerable<BuiltinSong> GetBuiltins()
    {
        foreach (var (usage, idcs) in UsesSongIndices)
        {
            IstringSet usageSet = [usage];
            foreach (var idx in idcs.Where(i => i < NumBuiltinSongs))
            {
                // Don't need to check for null names because they won't be in the usage lists
                yield return new(
                    idx,
                    $"Crystalis - {idx:x}. {_builtinSongNames[idx]!}",
                    "Yoko Osaka",
                    Uses: usageSet);
            }
        }
    }

    public Dictionary<int, ISong?> CreateMasterSongMap(
        IReadOnlyDictionary<string, List<ISong>> usesSongs,
        IShuffler shuffler)
    {
        var selUsesSongs = SelectUsesSongs(
            usesSongs, NumUsesSongs, shuffler);

        // Because of how Crystalis tracks loop and continue, the easiest way to handle built-in tracks is to not let them change track indices.
        Dictionary<int, ISong?> songMap = new();
        foreach (var (usage, songs) in selUsesSongs)
        {
            foreach (var song in songs.Where(s => s.IsEngine("native")))
                songMap[song.Number] = song;

            var songIdcs = UsesSongIndices[usage];
            int idxIdx = 0;
            foreach (var song in songs.Where(s => !s.IsEngine("native")))
            {
                while (songMap.ContainsKey(songIdcs[idxIdx]))
                    idxIdx++;

                songMap[songIdcs[idxIdx++]] = song;
            }
        }

        // Assign region tracks
        var rgnSongIdcs = CreateRegionSongMap(shuffler);
        AssignRegionSongs(rgnSongIdcs);

        AssignBossSongs(shuffler);

        Log.WriteLine("Selected Songs:");

        var tgtSongIdcs = songMap.Keys.ToList();
        tgtSongIdcs.Sort();

        foreach (var songIdx in tgtSongIdcs)
        {
            ISong song = songMap[songIdx]!;
            string authorStr = !string.IsNullOrEmpty(song.Author)
                ? $" ({song.Author})"
                : "";
            string songName = _builtinSongNames[songIdx]!;
            Log.WriteLine($"- {songIdx} ({songName}): {song.Title}{authorStr}");
        }

        if (rgnSongIdcs.Count != 0)
        {
            Log.WriteLine();
            Log.WriteLine("Selected Region Songs:");

            for (int rgnIdx = 0; rgnIdx < RegionInfos.Length; rgnIdx++)
            {
                if (rgnSongIdcs.TryGetValue(rgnIdx, out int songIdx))
                    Log.WriteLine($"- {RegionInfos[rgnIdx].Name}: {songMap[songIdx]!.Title}");
            }
        }

        return songMap;
    }

    byte[] GetAreaMusicIndicesFromAreaTable()
    {
        BinaryBuffer addrsBuff = new(new ArraySegment<byte>(
            Rom!,
            GetAreaTableAddressOffset(AreaTableAddress),
            AreaTableLength * 2));
        BinaryBuffer romBuff = new(Rom!);

        romBuff.Position = GetAreaTableAddressOffset(AreaTableAddress);

        var songIdcs = new byte[AreaTableLength];
        for (int areaIdx = 0; areaIdx < AreaTableLength; areaIdx++)
        {
            int infoAddr = addrsBuff.ReadUInt16LE();
            if (infoAddr < 0x8000 || infoAddr >= 0xc000)
                continue;

            romBuff.Position = GetAreaTableAddressOffset(infoAddr);
            int secAddr = romBuff.ReadUInt16LE();

            int offs = GetAreaTableAddressOffset(secAddr);
            songIdcs[areaIdx] = Rom![offs];
        }

        return songIdcs;
    }

    static int GetAreaTableAddressOffset(int address)
    {
        return AreaTable16kBankIndex * 0x4000 + address - 0x8000 + 0x10;
    }

    void PrintAreaSongIdcs()
    {
        if (Rom!.Length == 1)
            // Dummy ROM for testing libraries
            return;

        const int width = 8;
        var songIdcs = GetAreaMusicIndicesFromAreaTable();
        for (int areaIdx = 0; areaIdx < songIdcs.Length; areaIdx++)
        {
            int songIdx = songIdcs[areaIdx];
            string idxStr = $"${songIdx:x}";
            Debug.Write($"{idxStr,3}, ");
            if ((areaIdx + 1) % width == 0)
                Debug.WriteLine($"; {areaIdx - areaIdx % width:x}");
        }
    }

    Dictionary<int, int> CreateRegionSongMap(IShuffler shuffler)
    {
        IstringDictionary<List<int>> usesRgnIdcs = new(
            Uses.ToDictionary(u => u, u => new List<int>()));
        for (int rgnIdx = 0; rgnIdx < RegionInfos.Length; rgnIdx++)
            usesRgnIdcs[RegionInfos[rgnIdx].Usage].Add(rgnIdx);

        Dictionary<int, int> rgnSongIdcs = new();
        foreach (var (usage, rgnIdcs)
            in usesRgnIdcs.Where(kv => kv.Value.Count != 0))
        {
            var usageSongIdcs = UsesSongIndices[usage];
            int numSongs = usageSongIdcs.Length;
            int numReps = Math.Max((rgnIdcs.Count + numSongs - 1) / numSongs, 1);
            IList<int> selSongIdcs = Enumerable.Range(0, numReps)
                .SelectMany(x => usageSongIdcs)
                .ToArray();
            selSongIdcs = shuffler.Shuffle((IReadOnlyList<int>)selSongIdcs);

            foreach (var (rgnIdx, songIdx) in rgnIdcs.Zip(selSongIdcs))
                rgnSongIdcs[rgnIdx] = songIdx;
        }

        return rgnSongIdcs;
    }

    void AssignRegionSongs(IReadOnlyDictionary<int, int> regionSongIndices)
    {
        var areaSongMap = GetAreaMusicIndicesFromAreaTable();
        foreach (var (rgnIdx, songIdx) in regionSongIndices)
        {
            foreach (int areaIdx in RegionInfos[rgnIdx].TrackIndices)
                areaSongMap[areaIdx] = (byte)songIdx;
        }

        RomWriter.Write(AreaMusicTableOffset, areaSongMap, "Area Song Map");
    }

    void AssignBossSongs(IShuffler shuffler)
    {
        var songIdcs = UsesSongIndices["Boss"].Skip(1).ToArray();
        int numBossSongs = songIdcs.Length;
        int numReps = (BossMusicTableLength + numBossSongs - 1) / numBossSongs;
        IList<int> selSongIdcs = Enumerable.Range(0, numReps)
            .SelectMany(x => songIdcs)
            .ToArray();
        selSongIdcs = shuffler.Shuffle((IReadOnlyList<int>)selSongIdcs);

        RomWriter.Write(
            BossMusicTableOffset,
            selSongIdcs.Take(BossMusicTableLength).Select(x => checked((byte)x)).ToArray(),
            "Boss Song Map");
    }
}
