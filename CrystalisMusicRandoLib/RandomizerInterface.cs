using FtRandoLib.Importer;
using FtRandoLib.Utility;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CrystalisMusicRandoLib;

public static class RandomizerInterface
{
    static JsonSerializer _serializer = new JsonSerializer()
    {
        Formatting = Formatting.Indented,
    };

    public static (byte[] Rom, IEnumerable<int> FreeBanks, string Log) RandomizeRom(
        byte[] baseRom, 
        IReadOnlyList<int> freeBanks,
        int seed,
        int numRetries = 8,
        bool includeBuiltin = true,
        bool includeStandardLibrary = true,
        bool includeUnsafe = false,
        IReadOnlyList<string>? extraLibraries = null)
    {
        if (baseRom.Length != 0x60010 && baseRom.Length != 0xa0010)
            throw new ArgumentException("not a Crystalis ROM");

        if (!includeBuiltin
            && !includeStandardLibrary
            && (extraLibraries is null || extraLibraries.Count == 0))
            return (baseRom, freeBanks, "");

        var patchedRom = new byte[0xa0010];
        Array.Copy(baseRom, patchedRom, 0x3c010);
        Array.Copy(
            baseRom,
            baseRom.Length - 0x24000,
            patchedRom,
            patchedRom.Length - 0x24000,
            0x24000);

        byte[] patchData;
        using (var stream = OpenResource("Resources.crystalisft512kb.ips")!)
        {
            patchData = new byte[stream.Length];
            stream.ReadExactly(patchData, 0, checked((int)stream.Length));
        }

        IpsFile patch = new(patchData);
        patchedRom = patch.Apply(patchedRom);

        CrystalisImporter imptr = new(patchedRom, freeBanks);
        var allSongs = LoadSongs(
            imptr,
            includeBuiltin,
            includeStandardLibrary,
            extraLibraries,
            includeUnsafe);
        var usesSongs = imptr.SplitSongsByUsage(allSongs);

        Random seedRnd = new Random(seed);

        while (true)
        {
            var finalRom = patchedRom.ToArray();
            imptr = new(finalRom, freeBanks);

            Random rnd = new(seedRnd.Next());
            RandomShuffler shuffler = new(rnd);

            StringWriter logWriter = new();
            using TextLogger log = new(logWriter);
            using var logDispose = Log.Use(log);

            try
            {
                var songMap = imptr.CreateMasterSongMap(usesSongs, shuffler);

                imptr.Import(songMap, null, out var banksLeft);

                return (finalRom, banksLeft, logWriter.ToString());
            }
            catch (RomFullException)
            {
                if (numRetries-- <= 0)
                    throw;
            }
        }
    }

    public static void TestLibraries(string[] extraLibraries)
    {
        var dummyRom = new byte[1];
        CrystalisImporter imptr = new(dummyRom, [0]);

        var songs = LoadSongs(
            imptr, false, true, extraLibraries, true, true);
        imptr.TestRebase(songs);
    }

    public static string CreateResultJson(
        byte[] rom, 
        IReadOnlyCollection<int> freeBanks, 
        string log)
    {
        using (TextWriter writer = new StringWriter())
        {
            JsonSuccessResult res = new()
            {
                rom = rom,
                freeBanks = (freeBanks as int[]) ?? freeBanks.ToArray(),
                log = log,
            };
            _serializer.Serialize(writer, res, res.GetType());

            return writer.ToString()!;
        }
    }

    public static string CreateResultJson(string errorMessage)
    {
        using (TextWriter writer = new StringWriter())
        {
            JsonFailureResult res = new()
            {
                errorMessage = errorMessage,
            };
            _serializer.Serialize(writer, res, res.GetType());

            return writer.ToString()!;
        }
    }

    static Stream? OpenResource(string name)
    {
        var asm = Assembly.GetCallingAssembly();
        return asm.GetManifestResourceStream($"{asm.GetName().Name}.{name}");
    }

    static List<ISong> LoadSongs(
        CrystalisImporter imptr,
        bool includeBuiltin,
        bool includeStandardLibrary,
        IReadOnlyList<string>? extraLibraries = null,
        bool includeUnsafe = false,
        bool test = false)
    {
        List<ISong> songs = new();
        LibraryParserOptions parseOpts = new()
        {
            EnabledOnly = !test,
            IgnoreExtraFields = test,
            SafeOnly = !includeUnsafe,
        };

        if (includeBuiltin)
            songs.AddRange(imptr.GetBuiltins());

        if (includeStandardLibrary)
        {
            string jsonData = "";
            using (var stream = OpenResource("Resources.StandardLibrary.json5"))
            {
                using (StreamReader reader = new(stream!))
                    jsonData = reader.ReadToEnd();
            }

            songs.AddRange(
                imptr.LoadFtJsonLibrarySongs(jsonData, parseOpts));
        }

        if (extraLibraries is not null)
        {
            foreach (var libData in extraLibraries)
                songs.AddRange(
                    imptr.LoadFtJsonLibrarySongs(libData, parseOpts));
        }

        return songs;
    }
}
