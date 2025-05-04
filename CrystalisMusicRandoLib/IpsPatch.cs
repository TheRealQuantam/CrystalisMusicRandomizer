using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CrystalisMusicRandoLib;

internal record DiffRange(int StartOffs, int EndOffs)
{
    public int CompareTo(DiffRange other)
    {
        int endDiff = EndOffs.CompareTo(other.EndOffs);
        return (endDiff != 0)
            ? endDiff
            : StartOffs.CompareTo(other.StartOffs);
    }

    public override string ToString()
    {
        string endPart = (StartOffs + 1 != EndOffs)
            ? $"-{EndOffs - 1:x}"
            : "";
        return $"{StartOffs:x}{endPart}";
    }
}

internal class InvalidIpsPatch : Exception
{
    public InvalidIpsPatch()
        : base("invalid IPS patch")
    { }
}

internal class IncompleteIpsPatch : Exception
{
    public IncompleteIpsPatch()
        : base("incomplete IPS patch")
    { }
}

internal record IpsPatch(int SourceOffset, int TargetOffset, int Size, bool IsRle);

internal class IpsFile
{
    static readonly byte[] Signature = Encoding.ASCII.GetBytes("PATCH");
    static readonly byte[] EndSignature = Encoding.ASCII.GetBytes("EOF");

    public IReadOnlyList<IpsPatch> Patches => _patches;
    public byte[] Metadata => _data.Skip(_metaOffs).ToArray();

    IReadOnlyList<byte> _data;
    int _metaOffs;
    List<IpsPatch> _patches = new();

    public IpsFile(IReadOnlyList<byte> data)
    {
        _data = data;
        _metaOffs = 0;

        if (!_data.Take(Signature.Length).SequenceEqual(Signature))
            throw new ArgumentException("not an IPS patch");

        try
        {
            int offs = Signature.Length;
            while (!_data.Skip(offs).Take(EndSignature.Length).SequenceEqual(EndSignature))
            {
                int patchOffs = ReadNumber24(ref offs),
                    patchSize = ReadNumber16(ref offs),
                    nextOffs = checked(offs + patchSize);
                bool isRle = patchSize == 0;

                if (isRle)
                {
                    patchSize = ReadNumber16(ref offs);
                    nextOffs = offs + 1;
                }

                _patches.Add(new(offs, patchOffs, patchSize, isRle));

                offs = nextOffs;
            }

            _metaOffs = checked(offs + EndSignature.Length);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new IncompleteIpsPatch();
        }
    }

    public IEnumerable<DiffRange> GetDiff()
    {
        var patches = _patches.ToList();
        patches.Sort((a, b) =>
        {
            int cmp = a.TargetOffset.CompareTo(b.TargetOffset);
            return cmp != 0
                ? cmp
                : a.Size.CompareTo(b.Size);
        });

        return patches.Select(p => new DiffRange(
                p.TargetOffset, p.TargetOffset + p.Size));
    }

    public byte[] Apply(IReadOnlyList<byte> source, bool allowGaps = false)
    {
        if (_patches.Count == 0)
            return source.ToArray();

        var patchData = _data as byte[] ?? _data.ToArray();
        var buff = source.ToArray();
        int fileSize = buff.Length;

        foreach (var patch in _patches)
        {
            int endOffs = patch.TargetOffset + patch.Size;
            if (endOffs > buff.Length)
            {
                if (patch.TargetOffset > fileSize && !allowGaps)
                    throw new EndOfStreamException();

                int newSize = Math.Max(buff.Length, 1024);
                while (newSize < endOffs)
                    newSize = checked(newSize * 2);

                Array.Resize(ref buff, newSize);
            }

            if (patch.IsRle)
                Array.Fill(
                    buff, 
                    patchData[patch.SourceOffset], 
                    patch.TargetOffset, 
                    patch.Size);
            else
                Array.Copy(
                    patchData,
                    patch.SourceOffset,
                    buff,
                    patch.TargetOffset,
                    patch.Size);

            fileSize = Math.Max(fileSize, endOffs);
        }

        if (fileSize != buff.Length)
            Array.Resize(ref buff, fileSize);

        return buff;

        /*using (MemoryStream stream = new())
        {
            stream.Write(source.ToArray());

            var patchData = _data as byte[] ?? _data.ToArray();
            foreach (var patch in _patches)
            {
                if (patch.TargetOffset > stream.Length
                    && !allowExtend)
                    throw new EndOfStreamException();

                stream.Position = patch.TargetOffset;

                /*if (patch.IsRle)
                    stream.*/
                /*stream.Write(new(patchData, patch.SourceOffset, patch.Size));

            }
        }

        IpsPatch lastPatch = _patches[^1];
        if (!allowExtend
            && lastPatch.TargetOffset > source.Count)
            throw new EndOfStreamException();

        int tgtSize = lastPatch.TargetOffset + lastPatch.Size;
        var patched = Enumerable.Concat(
            source,
            Enumerable.Repeat((byte)0, tgtSize - source.Count)).ToArray();

        var patchData = _data as byte[] ?? _data.ToArray();
        foreach (var patch in _patches)
        {
            if (patch.IsRle)
                Array.Fill(
                    patched,
                    patchData[patch.SourceOffset],
                    patch.TargetOffset,
                    patch.Size);
            else
                Array.Copy(
                    patchData, 
                    patch.SourceOffset, 
                    patched, 
                    patch.TargetOffset, 
                    patch.Size);
        }

        return patched;*/
    }

    int ReadNumber16(ref int offs)
    {
        int x = ((int)_data[offs] << 8) | _data[offs + 1];

        offs += 2;

        return x;
    }

    int ReadNumber24(ref int offs)
    {
        int x = (int)_data[offs++] << 16;

        return x + ReadNumber16(ref offs);
    }
}
