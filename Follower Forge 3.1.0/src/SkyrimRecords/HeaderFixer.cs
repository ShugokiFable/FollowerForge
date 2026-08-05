namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Rewrites the TES4 HEDR num-records field to the Creation Kit convention
/// (count of all records AND GRUPs, excluding the TES4 record). Mutagen writes a
/// different count that makes the CK/ship-gate warn "form counts don't match".
/// This is an in-place uint32 patch — no sizes change, so it cannot corrupt the file.
/// </summary>
public static class HeaderFixer
{
    /// <summary>Patches the file at <paramref name="path"/>; returns the corrected count.</summary>
    public static uint FixRecordCount(string path)
    {
        var data = File.ReadAllBytes(path);
        var count = CountRecordsAndGrups(data);
        var hedr = IndexOf(data, "HEDR"u8);
        if (hedr < 0) throw new InvalidDataException($"{path}: no HEDR subrecord");
        // HEDR data = float version (4) + uint32 numRecords (4) + uint32 nextObjectId (4).
        var numRecordsPos = hedr + 6 + 4;
        BitConverter.TryWriteBytes(data.AsSpan(numRecordsPos, 4), count);
        File.WriteAllBytes(path, data);
        return count;
    }

    /// <summary>Same walk the ship-gate uses: every record + every GRUP, excluding TES4.</summary>
    private static uint CountRecordsAndGrups(byte[] data)
    {
        if (data.Length < 28 || !data.AsSpan(0, 4).SequenceEqual("TES4"u8))
            throw new InvalidDataException("Not a TES4 plugin");
        var tes4Size = BitConverter.ToUInt32(data, 4);
        var pos = 24 + (int)tes4Size;

        uint records = 0, grups = 0;

        void WalkRange(int start, int end)
        {
            var r = start;
            while (r + 24 <= end)
            {
                if (data.AsSpan(r, 4).SequenceEqual("GRUP"u8))
                {
                    grups++;
                    var gsz = BitConverter.ToUInt32(data, r + 4);
                    if (gsz < 24 || r + gsz > end)
                        throw new InvalidDataException($"corrupt nested GRUP at {r:X}");
                    WalkRange(r + 24, r + (int)gsz);
                    r += (int)gsz;
                }
                else
                {
                    var rdsz = BitConverter.ToUInt32(data, r + 4);
                    records++;
                    r += 24 + (int)rdsz;
                }
            }
        }

        while (pos + 24 <= data.Length)
        {
            if (!data.AsSpan(pos, 4).SequenceEqual("GRUP"u8)) break;
            grups++;
            var gsz = BitConverter.ToUInt32(data, pos + 4);
            if (gsz < 24 || pos + gsz > data.Length)
                throw new InvalidDataException($"corrupt top GRUP at {pos:X}");
            WalkRange(pos + 24, pos + (int)gsz);
            pos += (int)gsz;
        }
        return records + grups;
    }

    private static int IndexOf(byte[] haystack, ReadOnlySpan<byte> needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
                return i;
        return -1;
    }
}
