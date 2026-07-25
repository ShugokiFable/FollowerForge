using FollowerForge.Domain;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using Serilog;

namespace FollowerForge.SkyrimRecords;

/// <summary>
/// Deterministic follower compiler. Turns a <see cref="FollowerProfile"/> into a genuine
/// ESPFE (SkyrimMod, IsSmallMaster) with an NPC, follower factions, a player relationship,
/// and a placed exterior ACHR. FormIDs and EditorIDs are allocated in a fixed order so the
/// same profile always yields the same records.
/// </summary>
public sealed class FollowerCompiler(ILogger log)
{
    public sealed record CompileResult(
        SkyrimMod Mod,
        FormKey NpcFormKey,
        FormKey PlacedFormKey,
        string NpcEditorId,
        IReadOnlyList<string> Masters);

    /// <param name="profile">The follower to build.</param>
    /// <param name="location">
    /// Spawn point chosen from the location library. When null the follower falls back to the
    /// built-in Whiterun exterior spot (also the path unit tests use without a live game).
    /// </param>
    public CompileResult Compile(FollowerProfile profile, SpawnLocation? location,
        ICombatStyleGetter? combatStyleToClone = null, ICellGetter? placementCellSource = null)
    {
        var modKey = ModKey.FromFileName(profile.PluginName);
        var mod = new SkyrimMod(modKey, VanillaForms.Release) { IsSmallMaster = true };
        var prefix = SanitizePrefix(profile.EditorIdPrefix ?? profile.Name);

        // --- Allocation order is FIXED for determinism: NPC, [CSTY], RELA, ACHR ---
        var npc = mod.Npcs.AddNew($"{prefix}NPC");
        ConfigureNpc(npc, profile, prefix, mod, combatStyleToClone);

        var relationship = mod.Relationships.AddNew($"{prefix}Rel");
        relationship.Parent.SetTo(npc.FormKey);
        relationship.Child.SetTo(VanillaForms.PlayerNpc);
        relationship.Rank = Relationship.RankType.Ally;
        relationship.AssociationType.Clear();

        var placedKey = BuildPlacement(mod, npc, profile, location, placementCellSource);

        var masters = mod.ModHeader.MasterReferences.Select(m => m.Master.FileName.String).ToList();
        log.Information("Compiled follower {Name}: NPC {Npc}, ACHR {Achr}, masters [{Masters}]",
            profile.Name, npc.FormKey.ID.ToString("X6"), placedKey.ID.ToString("X6"),
            string.Join(", ", masters));

        return new CompileResult(mod, npc.FormKey, placedKey, npc.EditorID!, masters);
    }

    private void ConfigureNpc(Npc npc, FollowerProfile profile, string prefix, SkyrimMod mod,
        ICombatStyleGetter? combatStyleToClone)
    {
        npc.Name = profile.Name;
        npc.Race.SetTo(FormKey.Factory(profile.Race.FormKey));
        npc.Voice.SetTo(FormKey.Factory(profile.VoiceType.FormKey));
        npc.Class.SetTo(FormKey.Factory(profile.Class.FormKey));

        // Combat style: reference the existing one, or clone it into this plugin for tweaking.
        // Never edit the original CSTY — cloning duplicates all fields into a NEW record.
        if (profile.CombatStyle is { } cs)
        {
            if (cs.CloneIntoPlugin && combatStyleToClone is not null)
            {
                var clone = mod.CombatStyles.DuplicateInAsNewRecord(combatStyleToClone, $"{prefix}CSTY");
                npc.CombatStyle.SetTo(clone.FormKey);
            }
            else if (cs.CloneIntoPlugin)
            {
                // Requested a clone but the source could not be resolved: fall back to a reference
                // so the follower still gets the intended combat style (and log it).
                log.Warning("Combat style {Key} could not be resolved for cloning; referencing instead", cs.Style.FormKey);
                npc.CombatStyle.SetTo(FormKey.Factory(cs.Style.FormKey));
            }
            else
            {
                npc.CombatStyle.SetTo(FormKey.Factory(cs.Style.FormKey));
            }
        }

        // Outfit or explicit inventory.
        if (profile.Outfit is { } outfit)
            npc.DefaultOutfit.SetTo(FormKey.Factory(outfit.FormKey));
        foreach (var item in profile.InventoryItems)
            npc.Items!.Add(new ContainerEntry { Item = new ContainerItem { Item = FormKey.Factory(item.FormKey).ToLink<IItemGetter>(), Count = 1 } });

        foreach (var spell in profile.Spells)
            npc.ActorEffect!.Add(FormKey.Factory(spell.FormKey).ToLink<ISpellRecordGetter>());
        foreach (var perk in profile.Perks)
            npc.Perks!.Add(new PerkPlacement { Perk = FormKey.Factory(perk.FormKey).ToLink<IPerkGetter>(), Rank = 1 });

        // Shared-hub membership: carry the hub's marker keyword so the follower masters the hub.
        if (profile.Strategy == Domain.OutputStrategy.SharedHub && profile.HubPluginName is { } hub)
        {
            var hubKey = new FormKey(ModKey.FromFileName(hub), HubCompiler.MarkerKeywordId);
            npc.Keywords ??= [];
            npc.Keywords.Add(hubKey.ToLink<IKeywordGetter>());
        }

        // Follower factions: Potential rank 0 (can be hired), Current rank -1 (not yet following).
        npc.Factions.Add(new RankPlacement { Faction = VanillaForms.PotentialFollowerFaction.ToLink<IFactionGetter>(), Rank = 0 });
        npc.Factions.Add(new RankPlacement { Faction = VanillaForms.CurrentFollowerFaction.ToLink<IFactionGetter>(), Rank = -1 });
        npc.Factions.Add(new RankPlacement { Faction = VanillaForms.PlayerFaction.ToLink<IFactionGetter>(), Rank = 0 });

        // AI values.
        npc.AIData ??= new AIData();
        npc.AIData.Aggression = (Aggression)Math.Clamp(profile.Ai.Aggression, (byte)0, (byte)3);
        npc.AIData.Confidence = (Confidence)Math.Clamp(profile.Ai.Confidence, (byte)0, (byte)4);
        npc.AIData.Assistance = (Assistance)Math.Clamp(profile.Ai.Assistance, (byte)0, (byte)2);
        npc.AIData.Mood = Mood.Neutral;
        npc.AIData.Responsibility = (Responsibility)Math.Clamp(profile.Ai.Morality, (byte)0, (byte)3);
        npc.AIData.EnergyLevel = profile.Ai.Energy;

        // Level scaling.
        if (profile.Level.ScaleWithPlayer)
        {
            npc.Configuration.Level = new PcLevelMult
            {
                LevelMult = (ushort)Math.Clamp((int)(profile.Level.PlayerLevelMult * 1000), 0, ushort.MaxValue),
            };
            npc.Configuration.CalcMinLevel = profile.Level.MinLevel;
            npc.Configuration.CalcMaxLevel = profile.Level.MaxLevel;
        }
        else
        {
            npc.Configuration.Level = new NpcLevel { Level = profile.Level.FixedLevel };
        }

        // Configuration flags. Unique keeps FaceGen stable (no respawn/renumber).
        var flags = NpcConfiguration.Flag.Unique | NpcConfiguration.Flag.AutoCalcStats;
        if (profile.Female) flags |= NpcConfiguration.Flag.Female;
        if (profile.Protected) flags |= NpcConfiguration.Flag.Protected;
        if (profile.Essential) flags |= NpcConfiguration.Flag.Essential;
        npc.Configuration.Flags = flags;

        // A non-zero face tint colour keeps the NPC from defaulting to a grey face pre-FaceGen.
        npc.TextureLighting = System.Drawing.Color.FromArgb(255, 255, 255, 255);
    }

    /// <summary>
    /// Builds the placed ACHR. Adds a minimal override of WhiterunWorld's persistent cell
    /// (01A270) containing one new persistent reference — additive, so vanilla references are
    /// untouched. Masters: Skyrim.esm only (plus whatever the NPC's own links require).
    /// </summary>
    private FormKey BuildPlacement(SkyrimMod mod, Npc npc, FollowerProfile profile,
        SpawnLocation? location, ICellGetter? cellSource)
    {
        var pos = profile.Placement;
        var placed = new PlacedNpc(mod.GetNextFormKey(), VanillaForms.Release)
        {
            EditorID = $"{SanitizePrefix(profile.EditorIdPrefix ?? profile.Name)}Ref",
            Base = new FormLinkNullable<INpcGetter>(npc.FormKey),
        };

        if (location is not null)
        {
            // Chosen from the library: use the coordinates a shipped mod already proved.
            placed.Placement = new Placement
            {
                Position = new P3Float(location.X, location.Y, location.Z),
                Rotation = new P3Float(0, 0, location.RotationZ),
            };
            CellPlacer.Place(mod, location, placed, cellSource);
            log.Information("Placed {Name} at {Place} ({Kind})", profile.Name, location.Display, location.Kind);
            return placed.FormKey;
        }

        // No library entry: fall back to the built-in Whiterun exterior spot.
        placed.Placement = new Placement
        {
            Position = pos.Cell is null
                ? new P3Float(VanillaForms.WhiterunDefaultPos.X, VanillaForms.WhiterunDefaultPos.Y, VanillaForms.WhiterunDefaultPos.Z)
                : new P3Float(pos.X, pos.Y, pos.Z),
            Rotation = new P3Float(0, 0, pos.AngleZDeg * MathF.PI / 180f),
        };
        CellPlacer.PlaceInWorldspace(mod, VanillaForms.WhiterunWorld,
            VanillaForms.WhiterunWorldPersistentCell, placed, cellSource);
        return placed.FormKey;
    }

    private static string SanitizePrefix(string name)
    {
        var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c)).ToArray());
        if (cleaned.Length == 0) cleaned = "Follower";
        return "FF_" + cleaned + "_";
    }
}
