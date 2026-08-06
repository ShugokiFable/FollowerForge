using FollowerForge.Domain;

namespace FollowerForge.Validation;

/// <summary>
/// In-process port of the skyrim-ship-gate verifier: HEDR 1.71, TES4 + every record
/// formVersion 44, ESL flag 0x200, new FormIDs in 0x800–0xFFF, HEDR count = records + GRUPs.
/// Runs on the exact bytes FollowerForge is about to ship.
/// </summary>
public static class EspHeaderValidator
{
    public static void Validate(string path, ValidationReport report, bool requireEsl = true)
    {
        byte[] data;
        try
        {
            data = File.ReadAllBytes(path);
        }
        catch (Exception ex)
        {
            report.Add(ValidationSeverity.Error, "ESP_READ", $"Cannot read plugin: {ex.Message}", path);
            return;
        }

        if (data.Length < 28 || !data.AsSpan(0, 4).SequenceEqual("TES4"u8))
        {
            report.Add(ValidationSeverity.Error, "ESP_NOT_TES4", "Not a TES4 plugin", path);
            return;
        }

        var flags = BitConverter.ToUInt32(data, 8);
        var tes4Fv = BitConverter.ToUInt16(data, 20);
        if (tes4Fv != 44)
            report.Add(ValidationSeverity.Error, "ESP_TES4_FV",
                $"TES4 formVersion must be 44, got {tes4Fv}", path);

        if (requireEsl && (flags & 0x200) == 0)
            report.Add(ValidationSeverity.Error, "ESP_NO_ESL",
                $"Missing ESL/ESPFE flag 0x200 (flags={flags:X})", path);

        var hedr = IndexOf(data, "HEDR"u8);
        if (hedr < 0)
        {
            report.Add(ValidationSeverity.Error, "ESP_NO_HEDR", "Missing HEDR", path);
            return;
        }
        var hedrVer = BitConverter.ToSingle(data, hedr + 6);
        var nrec = BitConverter.ToUInt32(data, hedr + 6 + 4);
        if (Math.Abs(hedrVer - 1.71f) >= 0.001f)
            report.Add(ValidationSeverity.Error, "ESP_HEDR_VER",
                $"HEDR must be 1.71, got {hedrVer}", path);

        var tes4Size = BitConverter.ToUInt32(data, 4);
        var numMasters = CountMasters(data, (int)tes4Size);
        var esl = (flags & 0x200) != 0;

        uint records = 0, grups = 0;
        var badFv = new List<string>();
        var badId = new List<string>();

        void Walk(int start, int end)
        {
            var r = start;
            while (r + 24 <= end)
            {
                if (data.AsSpan(r, 4).SequenceEqual("GRUP"u8))
                {
                    grups++;
                    var gsz = BitConverter.ToUInt32(data, r + 4);
                    if (gsz < 24 || r + gsz > end)
                    {
                        report.Add(ValidationSeverity.Error, "ESP_GRUP", $"Corrupt nested GRUP at {r:X}", path);
                        return;
                    }
                    Walk(r + 24, r + (int)gsz);
                    r += (int)gsz;
                }
                else
                {
                    var rdsz = BitConverter.ToUInt32(data, r + 4);
                    var rfid = BitConverter.ToUInt32(data, r + 12);
                    var rfv = BitConverter.ToUInt16(data, r + 20);
                    records++;
                    if (rfv != 44)
                        badFv.Add($"{System.Text.Encoding.Latin1.GetString(data, r, 4)}:{rfid:X8}={rfv}");
                    if (esl && rfid != 0)
                    {
                        var masterIndex = (rfid >> 24) & 0xFF;
                        var localId = rfid & 0xFFFFFF;
                        if (masterIndex == numMasters && !(localId is >= 0x800 and <= 0xFFF))
                            badId.Add($"{rfid:X8}");
                    }
                    r += 24 + (int)rdsz;
                }
            }
        }

        var pos = 24 + (int)tes4Size;
        while (pos + 24 <= data.Length)
        {
            if (!data.AsSpan(pos, 4).SequenceEqual("GRUP"u8)) break;
            grups++;
            var gsz = BitConverter.ToUInt32(data, pos + 4);
            if (gsz < 24 || pos + gsz > data.Length)
            {
                report.Add(ValidationSeverity.Error, "ESP_GRUP", $"Corrupt top GRUP at {pos:X}", path);
                break;
            }
            Walk(pos + 24, pos + (int)gsz);
            pos += (int)gsz;
        }

        if (records == 0)
            report.Add(ValidationSeverity.Error, "ESP_NO_RECORDS", "No records found", path);
        if (badFv.Count > 0)
            report.Add(ValidationSeverity.Error, "ESP_FV_44",
                $"formVersion != 44: {string.Join(", ", badFv.Take(10))}", path);
        if (badId.Count > 0)
            report.Add(ValidationSeverity.Error, "ESP_LIGHT_ID",
                $"New light FormIDs outside 0x800-0xFFF: {string.Join(", ", badId.Take(10))}", path);
        if (nrec != records + grups)
            report.Add(ValidationSeverity.Error, "ESP_HEDR_COUNT",
                $"HEDR numRecords={nrec} != records({records})+GRUPs({grups})={records + grups}", path);

        if (!report.HasErrors)
            report.Add(ValidationSeverity.Info, "SHIP_GATE_PASS",
                $"HEDR={hedrVer} numRec={nrec} formVersion=44 ({records} records) ESL={esl}", path);
    }

    private static int CountMasters(byte[] data, int tes4Size)
    {
        var count = 0;
        var tp = 24;
        var end = 24 + tes4Size;
        while (tp + 6 <= end)
        {
            var sig = data.AsSpan(tp, 4);
            var tsz = BitConverter.ToUInt16(data, tp + 4);
            if (sig.SequenceEqual("MAST"u8)) count++;
            tp += 6 + tsz;
        }
        return count;
    }

    private static int IndexOf(byte[] haystack, ReadOnlySpan<byte> needle)
    {
        for (var i = 0; i <= haystack.Length - needle.Length; i++)
            if (haystack.AsSpan(i, needle.Length).SequenceEqual(needle))
                return i;
        return -1;
    }
}
