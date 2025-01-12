using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PKHeX.EncounterSlotDumper.Properties;

namespace PKHeX.EncounterSlotDumper;

public static class Dumper3
{
    public static void DumpGen3()
    {
        var r = Resources.encounter_r;
        var s = Resources.encounter_s;
        var e = Resources.encounter_e;

        var f = Resources.encounter_fr;
        var l = Resources.encounter_lg;

        var ru = EncounterArea3.GetArray3(BinLinker.Unpack(r, "ru"));
        var sa = EncounterArea3.GetArray3(BinLinker.Unpack(s, "sa"));
        var em = EncounterArea3.GetArray3(BinLinker.Unpack(e, "em"));
        EncounterUtil.MarkEncountersStaticMagnetPull(em, PersonalTable.E);

        var fr = EncounterArea3.GetArray3(BinLinker.Unpack(f, "fr"));
        var lg = EncounterArea3.GetArray3(BinLinker.Unpack(l, "lg"));

        // Remove unreleased Altering Cave tables
        fr = fr.Where(z => z.Location != 183 || z.Slots[0].Species == (int)Species.Zubat).ToArray();
        lg = lg.Where(z => z.Location != 183 || z.Slots[0].Species == (int)Species.Zubat).ToArray();
        em = em.Where(z => z.Location != 210 || z.Slots[0].Species == (int)Species.Zubat).ToArray();

        var rd = ru.Concat([FishFeebas]).OrderBy(z => z.Location).ThenBy(z => z.Type);
        var sd = sa.Concat([FishFeebas]).OrderBy(z => z.Location).ThenBy(z => z.Type);
        var ed = em.Concat([FishFeebas]).OrderBy(z => z.Location).ThenBy(z => z.Type);

        var fd = fr.OrderBy(z => z.Location).ThenBy(z => z.Type);
        var ld = lg.OrderBy(z => z.Location).ThenBy(z => z.Type);

        Write(rd, "encounter_r.pkl", "ru");
        Write(sd, "encounter_s.pkl", "sa");
        Write(ed, "encounter_e.pkl", "em");
        Write(fd, "encounter_fr.pkl", "fr");
        Write(ld, "encounter_lg.pkl", "lg");

        WriteSwarm(SlotsRSEAlt, "encounter_rse_swarm.pkl", "rs");
    }

    public static void Write(IEnumerable<EncounterArea3> area, string name, string ident = "g3")
    {
        var serialized = area.Select(Write).ToArray();
        List<byte[]> unique = [];
        foreach (var a in serialized)
        {
            if (unique.Any(z => z.SequenceEqual(a)))
                continue;
            unique.Add(a);
        }

        var packed = BinLinker.Pack([.. unique], ident);
        File.WriteAllBytes(name, packed);
        Console.WriteLine($"Wrote {name} with {unique.Count} unique tables (originally {serialized.Length}).");
    }

    public static byte[] Write(EncounterArea3 area)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write((ushort)area.Location);
        bw.Write((byte)area.Type);
        bw.Write((byte)area.Rate);

        foreach (var slot in area.Slots)
            WriteSlot(bw, slot);

        return ms.ToArray();
    }

    private static void WriteSlot(BinaryWriter bw, EncounterSlot3 slot)
    {
        bw.Write(slot.Species);
        bw.Write(slot.Form);
        bw.Write(slot.SlotNumber);
        bw.Write(slot.LevelMin);
        bw.Write(slot.LevelMax);
        bw.Write(slot.MagnetPullIndex);
        bw.Write(slot.MagnetPullCount);
        bw.Write(slot.StaticIndex);
        bw.Write(slot.StaticCount);
    }

    public static void WriteSwarm(IEnumerable<EncounterArea3> area, string name, string ident = "g3")
    {
        var serialized = area.Select(WriteSwarm).ToArray();
        var packed = BinLinker.Pack(serialized, ident);
        File.WriteAllBytes(name, packed);
    }

    public static byte[] WriteSwarm(EncounterArea3 area)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        bw.Write((ushort)area.Location);
        bw.Write((byte)area.Type);
        bw.Write((byte)area.Rate);

        foreach (var slot in area.Slots.Cast<EncounterSlot3Swarm>())
            WriteSlotSwarm(bw, slot);

        return ms.ToArray();
    }

    private static void WriteSlotSwarm(BinaryWriter bw, EncounterSlot3Swarm slot)
    {
        bw.Write(slot.Species);
        bw.Write(slot.Form);
        bw.Write(slot.SlotNumber);
        bw.Write(slot.LevelMin);
        bw.Write(slot.LevelMax);
        foreach (var move in slot.Moves.Span)
            bw.Write(move);

        // Magnet/Static are all 0's; don't bother.
    }

    private static ReadOnlySpan<ushort> MoveSwarmSurskit => [145, 098, 000, 000]; /* Bubble, Quick Attack */
    private static ReadOnlySpan<ushort> MoveSwarmSeedot => [117, 106, 073, 000];  /* Bide, Harden, Leech Seed */
    private static ReadOnlySpan<ushort> MoveSwarmNuzleaf => [106, 074, 267, 073]; /* Harden, Growth, Nature Power, Leech Seed */
    private static ReadOnlySpan<ushort> MoveSwarmSeedotF => [202, 218, 076, 073]; /* Giga Drain, Frustration, Solar Beam, Leech Seed */
    private static ReadOnlySpan<ushort> MoveSwarmSkittyRS => [045, 033, 000, 000]; /* Growl, Tackle */
    private static ReadOnlySpan<ushort> MoveSwarmSkittyE => [045, 033, 039, 213]; /* Growl, Tackle, Tail Whip, Attract */

    private static readonly EncounterArea3[] SlotsRSEAlt =
    [
        // Swarm can be passed from R/S<->E via mixing records
        // Encounter Percent is a 50% call
        new() {
            Location = 17, // Route 102
            Type = SlotType3.SwarmGrass50,
            Rate = 20,
            Slots =
            [
                new EncounterSlot3Swarm(MoveSwarmSurskit) { Species = 283, LevelMin = 03, LevelMax = 03 },
                new EncounterSlot3Swarm(MoveSwarmSeedot) { Species = 273, LevelMin = 03, LevelMax = 03 }
            ],},
        new() { Location = 29, // Route 114
            Type = SlotType3.SwarmGrass50,
            Rate = 20,
            Slots =
            [
                new EncounterSlot3Swarm(MoveSwarmSurskit) { Species = 283, LevelMin = 15, LevelMax = 15 },
                new EncounterSlot3Swarm(MoveSwarmNuzleaf) { Species = 274, LevelMin = 15, LevelMax = 15 }
            ],},
        new() { Location = 31, // Route 116
            Type = SlotType3.SwarmGrass50,
            Rate = 20,
            Slots =
            [
                new EncounterSlot3Swarm(MoveSwarmSkittyRS) { Species = 300, LevelMin = 15, LevelMax = 15 },
                new EncounterSlot3Swarm(MoveSwarmSkittyE) { Species = 300, LevelMin = 08, LevelMax = 08 }
            ],},
        new() { Location = 32, // Route 117
            Type = SlotType3.SwarmGrass50,
            Rate = 20,
            Slots =
            [
                new EncounterSlot3Swarm(MoveSwarmSurskit) { Species = 283, LevelMin = 15, LevelMax = 15 },
                new EncounterSlot3Swarm(MoveSwarmNuzleaf) { Species = 273, LevelMin = 13, LevelMax = 13 } // Has same moves as Nuzleaf
            ],},
        new() { Location = 35, // Route 120
            Type = SlotType3.SwarmGrass50,
            Rate = 20,
            Slots =
            [
                new EncounterSlot3Swarm(MoveSwarmSurskit) { Species = 283, LevelMin = 28, LevelMax = 28},
                new EncounterSlot3Swarm(MoveSwarmSeedotF) { Species = 273, LevelMin = 25, LevelMax = 25}
            ],}
    ];

    // Feebas fishing spot
    private static readonly EncounterArea3 FishFeebas = new()
    {
        Location = 34, // Route 119
        Rate = 0,
        Type = SlotType3.SwarmFish50,
        Slots =
        [
            new EncounterSlot3 {Species = 349, LevelMin = 20, LevelMax = 25} // Feebas with any Rod (50%)
        ],
    };
}
