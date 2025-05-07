using FtRandoLib.Importer;
using FtRandoLib.Library;
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
        bool includeDiverse = false,
        bool includeUnsafe = false,
        IReadOnlyList<string>? extraLibraries = null)
    {
        if (baseRom.Length != 0x60010 && baseRom.Length != 0xa0010)
            throw new ArgumentException("not a Crystalis ROM");

        if (!includeBuiltin
            && !includeStandardLibrary
            && (extraLibraries is null || extraLibraries.Count == 0))
            return (baseRom, freeBanks, "");

        byte[] patchedRom;
        if (baseRom.Length == 0x60010)
        {
            patchedRom = new byte[0xa0010];

            Array.Copy(baseRom, patchedRom, 0x3c010);
            Array.Copy(
                baseRom,
                baseRom.Length - 0x24000,
                patchedRom,
                patchedRom.Length - 0x24000,
                0x24000);
        }
        else
            patchedRom = baseRom.ToArray();

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
            includeBuiltin: includeBuiltin,
            includeStandardLibrary: includeStandardLibrary,
            extraLibraries: extraLibraries,
            includeDiverse: includeDiverse,
            includeUnsafe: includeUnsafe);
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
            imptr: imptr, 
            includeBuiltin: false, 
            includeStandardLibrary: true, 
            extraLibraries: extraLibraries, 
            includeDiverse: true, 
            includeUnsafe: true, 
            test: true);
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
        bool includeDiverse = false,
        bool includeUnsafe = false,
        bool test = false)
    {
        List<ISong> songs = new();
        LibraryParserOptions parseOpts = new()
        {
            EnabledOnly = !test,
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

            songs.AddRange(LoadLibrarySongs(
                imptr, "standard library", jsonData, parseOpts, includeDiverse));
        }

        if (extraLibraries is not null)
        {
            for (int i = 0; i < extraLibraries.Count; i++)
                songs.AddRange(LoadLibrarySongs(
                    imptr, 
                    $"custom library {i + 1}",
                    extraLibraries[i], 
                    parseOpts, 
                    includeDiverse));
        }

        return songs;
    }

    static IEnumerable<ISong> LoadLibrarySongs(
        CrystalisImporter imptr,
        string libraryName,
        string libraryData,
        LibraryParserOptions parseOptions,
        bool includeDiverse)
    {
        try
        {
            var libEnum = imptr.LoadFtJsonLibrarySongs(libraryData, parseOptions);
            return includeDiverse
                ? libEnum
                : libEnum.Where(s => !s.Tags.Contains("diverse"));
        }
        catch (ParsingError ex)
        {
            StringBuilder sb = new();
            sb.AppendLine($"Error loading custom music from '{libraryName}'");

            string? atStr = ex.AtString;
            if (atStr is not null)
                sb.AppendLine(atStr);

            sb.AppendLine();

            sb.AppendLine(ex.Message);
            if (ex.Submessage is not null)
                sb.AppendLine(ex.Submessage);

            throw new Exception(sb.ToString(), ex);
        }
    }
}
