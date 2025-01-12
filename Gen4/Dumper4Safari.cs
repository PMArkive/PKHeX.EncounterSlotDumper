using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PKHeX.EncounterSlotDumper.Properties;
using static PKHeX.EncounterSlotDumper.SlotType4;

namespace PKHeX.EncounterSlotDumper;

public static class Dumper4Safari
{
    public static readonly List<string> Parse = [];
    public static bool ExportParse { get; set; } = true;

    private const byte SafariZoneMetLocation = 202;

    public static EncounterArea4HGSS[] GetSafariAreaSets()
    {
        var resource = Resources.safari_a230;
        var pkl = BinLinker.Unpack(resource, "g4");
        var zones = new SafariZone[pkl.Length];
        for (int i = 0; i < pkl.Length; i++)
            zones[i] = new SafariZone(pkl[i]);

        // Dump zones to parse
        if (ExportParse)
            SafariZoneDumper.DumpZones(zones);

        // subzone, and by type
        var computedTables = new List<EncounterArea4HGSS>();
        for (int i = 0; i < zones.Length; i++)
            AddToTable(computedTables, (SafariSubZone4)i, zones[i]);

        return [.. computedTables];
    }

    private static void AddToTable(List<EncounterArea4HGSS> result, SafariSubZone4 i, SafariZone zone)
    {
        AddToTable(result, i, zone.Grass);
        AddToTable(result, i, zone.Surf);
        AddToTable(result, i, zone.Old);
        AddToTable(result, i, zone.Good);
        AddToTable(result, i, zone.Super);
    }

    private static void AddToTable(List<EncounterArea4HGSS> result, SafariSubZone4 index, SafariSlotSet set)
    {
        var type = set.Type;
        bool hasWater = index.IsWater();
        if (!hasWater && type != Safari_Grass)
            return;

        if (ExportParse)
            Parse.Add($"Safari {index} {type}");

        var deduplicate = new HashSet<SafariSlot4>();
        AddAndRunPermutation(deduplicate, set.Day, set.ExtraDay, set.ExtraBlocks, "Day");
        AddAndRunPermutation(deduplicate, set.Morning, set.ExtraMorning, set.ExtraBlocks, "Morning");
        AddAndRunPermutation(deduplicate, set.Night, set.ExtraNight, set.ExtraBlocks, "Night");
        if (deduplicate.Count == 0)
            throw new Exception($"No slots found for {index} {type}");

        var zipped = GetAreaWrapper(deduplicate, type, index);
        CheckContains(set.ExtraDay, zipped);
        CheckContains(set.ExtraMorning, zipped);
        CheckContains(set.ExtraNight, zipped);
        result.Add(zipped);
    }

    // Assert that all extra forms were added with at least one definition.
    private static void CheckContains(EncounterSlot4[] extra, EncounterArea4HGSS area)
    {
        foreach (var expect in extra)
        {
            if (!area.ContainsSpeciesForm(expect))
                throw new Exception($"Missing slot {expect}");
        }
    }

    private static EncounterArea4HGSS GetAreaWrapper(IEnumerable<SafariSlot4> deduplicate, SlotType4 type, SafariSubZone4 index)
    {
        var condensed = deduplicate
            .OrderBy(z => z.Species).ThenBy(z => z.Level)
            .ThenBy(z => z.SlotNumber)
            .ThenBy(z => z.MagnetPullCount)
            .ThenBy(z => z.StaticCount)
            .Select(z => z.Inflate()).ToArray();

        return new EncounterArea4HGSS
        {
            Location = SafariZoneMetLocation,
            Type = type,
            Slots = condensed,
            Rate = (byte)index,
        };
    }

    private static void AddAndRunPermutation(HashSet<SafariSlot4> result, EncounterSlot4[] regular,
        EncounterSlot4[] extra, BlockRequirement[] blocks, string time)
    {
        if (ExportParse)
            Parse.Add($"===={time}====");

        // Create all hypothetical permutations of the extra slots.
        // For each valid permutation, add the result, and run the magnet pull/static slot calc on it too.
        CreatePermutations(result, regular, extra, blocks);
    }

    private static void CreatePermutations(HashSet<SafariSlot4> result, EncounterSlot4[] regular, EncounterSlot4[] extra, BlockRequirement[] blocks)
    {
        EncounterSlot4[] workspace = [.. regular]; // keep existing slots
        Span<bool> used = stackalloc bool[extra.Length];
        Span<byte> placed = stackalloc byte[(int)SafariBlockType4.COUNT]; // 1,2,3,4 valid indexes

        // Here we go!
        // Un-permuted is a valid setup. Export it.
        AddPermutationAndDerived(result, workspace, placed);
        PermuteAdd(result, extra, blocks, workspace, used, placed);
    }

    private static void PermuteAdd(HashSet<SafariSlot4> result, ReadOnlySpan<EncounterSlot4> extra, ReadOnlySpan<BlockRequirement> blocks,
        EncounterSlot4[] workspace, Span<bool> used, Span<byte> placed, byte slotIndex = 0, int depth = 0)
    {
        if (extra.Length == depth)
            return; // end of the line

        // We can either use or not use the current slot.
        // Try skipping and adding a lower priority slot.
        PermuteAdd(result, extra, blocks, workspace, used, placed, slotIndex, depth + 1);

        // Try adding the current slot.
        used[depth] = true;
        var prior = workspace[slotIndex];
        workspace[slotIndex] = extra[depth] with { SlotNumber = slotIndex };

        // Update the current blocks placed to ensure the slot is added.
        Span<byte> newPlaced = stackalloc byte[placed.Length];
        placed.CopyTo(newPlaced);
        UpdateRequirements(newPlaced, blocks[depth]);

        // Check if no higher-index slots are made valid -- deeper recursion will enable them naturally.
        var chk = IsPermutationValid(newPlaced, blocks, used, depth);
        if (chk != ArrangeResult.Invalid)
        {
            // Only export if no side effects (higher index slots)
            if (chk == ArrangeResult.Valid)
                AddPermutationAndDerived(result, workspace, newPlaced);
            // else a deeper slot was needing to be true, we'll attempt it later.

            // DEEPER: Continue adding more slots.
            PermuteAdd(result, extra, blocks, workspace, used, newPlaced, (byte)(slotIndex + 1), depth + 1);
        }

        // Revert changes.
        used[depth] = false;
        workspace[slotIndex] = prior;
    }

    private enum ArrangeResult
    {
        /// <summary>
        /// All slots are valid with required blocks.
        /// </summary>
        Valid,

        /// <summary>
        /// One or more required FALSE slots has its requirements met.
        /// </summary>
        Invalid,

        /// <summary>
        /// One or more required FALSE slots has its requirements met, but it is a deeper slot that will be checked later.
        /// </summary>
        SideEffect,
    }

    private static ArrangeResult IsPermutationValid(ReadOnlySpan<byte> placed, ReadOnlySpan<BlockRequirement> blocks, Span<bool> used, int depth)
    {
        // If a slot was not used, then its block requirements must not be met.
        // Slots after can still be replaced if satisfied, rendering our arrangement impossible.
        // Ensure all extra block requirements are matching the used state.
        for (int i = 0; i < used.Length; i++)
        {
            if (used[i])
                continue;
            if (!blocks[i].IsSatisfied(placed))
                continue;

            if (i < depth) // Slot at this index should really be TRUE, so our setup is invalid.
                return ArrangeResult.Invalid;
            // Later slot can be flipped on via the recursion, so just indicate that it's not a valid setup yet.
            return ArrangeResult.SideEffect;
        }
        return ArrangeResult.Valid;
    }

    private static void UpdateRequirements(Span<byte> placed, BlockRequirement block)
    {
        AddBlock(placed, block.Block0, block.Count0);
        AddBlock(placed, block.Block1, block.Count1);
    }

    private static void AddBlock(Span<byte> newPlaced, byte type, byte count)
    {
        ref var existCount = ref newPlaced[type];
        existCount = Math.Max(existCount, count);
    }

    private static void AddPermutationAndDerived(HashSet<SafariSlot4> result, EncounterSlot4[] workspace, ReadOnlySpan<byte> blocks)
    {
        foreach (var slot in workspace)
            result.Add(Slim(slot));

        if (ExportParse)
            AddToParse(workspace, blocks);

        // Run the magnet pull/static slot calc on it too.
        List<EncounterSlot4> permuted = [];
        EncounterUtil.MarkEncountersStaticMagnetPullPermutation(workspace, PersonalTable.HGSS, permuted);
        foreach (var slot in permuted)
            result.Add(Slim(slot));
        if (permuted.Count != 0 && ExportParse)
            AddToParseMagStat(permuted);
    }

    private static void AddToParseMagStat(List<EncounterSlot4> slots)
    {
        foreach (var slot in slots)
        {
            if (slot.StaticCount != 0)
                Parse.Add($"\t{slot.LevelMin} Static {slot.StaticIndex}/{slot.StaticCount}: {(Species)slot.Species}");
        }
        foreach (var slot in slots)
        {
            if (slot.MagnetPullCount != 0)
                Parse.Add($"\t{slot.LevelMin} Magnet: {slot.MagnetPullIndex}/{slot.MagnetPullCount}: {(Species)slot.Species}");
        }
    }

    private static void AddToParse(EncounterSlot4[] workspace, ReadOnlySpan<byte> blocks)
    {
        var blockString = GetBlockString(blocks);
        Parse.Add($"Blocks: {blockString}");
        foreach (var slot in workspace)
            Parse.Add($"\t{slot.LevelMin} {slot.SlotNumber} {(Species)slot.Species}");
    }

    private static string GetBlockString(ReadOnlySpan<byte> blocks)
    {
        var allZero = blocks.IndexOfAnyExcept<byte>(0);
        if (allZero == -1)
            return "None";

        var sb = new StringBuilder();
        for (int i = 1; i < blocks.Length; i++)
        {
            if (blocks[i] == 0)
                continue;
            if (sb.Length != 0)
                sb.Append(", ");
            sb.Append($"{(SafariBlockType4)i} x{blocks[i]}");
        }
        return sb.ToString();
    }

    private static SafariSlot4 Slim(EncounterSlot4 arg) => new(arg);
}
