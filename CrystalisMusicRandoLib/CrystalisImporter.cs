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

namespace CrystalisMusicRandoLib;

internal class CrystalisImporter : Importer
{
    static int _bankSize = 0x2000;
    protected override int BankSize => _bankSize;
    protected override List<int> FreeBanks { get; }

    protected override int PrimarySquareChan => 0;
    protected override IstringSet Uses { get; } = new(UseSongIndices.Keys);
    protected override IstringSet DefaultUses { get; } = ["Dungeon"];
    protected override bool DefaultStreamingSafe => true;

    protected override int SongMapOffs => 0x32010 + 0x10;
    protected override int SongModAddrTblOffs => 0x320b0 + 0x10;

    static Range[] _builtInSongIdxRngs = [new(0, 2), new(3, 5), new(6, 0x10), new(0x11, 0x20)];
    static readonly int _numSongs = 0x4f;
    protected override HashSet<int> BuiltinSongIdcs { get; } = _builtInSongIdxRngs.SelectMany(r => Enumerable.Range(r.Start.Value, r.End.Value - r.Start.Value)).ToHashSet();
    protected override List<int> FreeSongIdcs => Enumerable.Concat(
        [2, 5, 0x10], 
        Enumerable.Range(0x20, _numSongs - 0x20)).ToList();
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
    ];
    static Dictionary<string, int[]> UseSongIndices = new()
    {
        { "Overworld", [1, 4] },
        { "Dungeon", [3, 7, 8, 0x11, 0x15, 0x17, 0x18, 0x19] },
        { "Ship", [6] },
        { "Draygon", [0xc] },
        { "Mesia", [0x1b] },
        { "Town", [0xd, 0x16] },
        { "Boss", [0x12] },
        { "Ending", [0x1c] },
    };
    static Dictionary<string, int> NumUsesSongs = UseSongIndices.ToDictionary(kv => kv.Key, kv => kv.Value.Length);

    public CrystalisImporter(byte[] rom, IReadOnlyList<int> freeBanks)
        : base(_bankLayouts, new SimpleRomAccess(rom))
    {
        FreeBanks = freeBanks.ToList();
    }

    public IEnumerable<BuiltinSong> GetBuiltins()
    {
        foreach (var (usage, idcs) in UseSongIndices)
        {
            IstringSet usageSet = [usage];
            foreach (var idx in idcs)
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

            var songIdcs = UseSongIndices[usage];
            int idxIdx = 0;
            foreach (var song in songs.Where(s => !s.IsEngine("native")))
            {
                while (songMap.ContainsKey(songIdcs[idxIdx]))
                    idxIdx++;

                songMap[songIdcs[idxIdx++]] = song;
            }
        }

        var mapSongIdcs = songMap.Keys.ToList();
        mapSongIdcs.Sort();

        Log.WriteLine("Selected Songs:");
        foreach (var songIdx in mapSongIdcs)
        {
            ISong song = songMap[songIdx]!;
            string authorStr = !string.IsNullOrEmpty(song.Author)
                ? $" ({song.Author})"
                : "";
            Log.WriteLine($"- {songIdx} ({_builtinSongNames[songIdx]}): {song.Title}{authorStr}");
        }

        return songMap;
    }
}
