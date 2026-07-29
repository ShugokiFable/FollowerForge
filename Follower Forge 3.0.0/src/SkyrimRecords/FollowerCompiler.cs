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
        IReadOnlyList<string> Masters,
        DialogueCompiler.DialogueResult? Dialogue = null,
        /// <summary>Non-null only when the experimental evolution script was attached.</summary>
        EvolutionCompiler.EvolutionResult? Evolution = null,
        /// <summary>Non-null only when the experimental transformation script was attached.</summary>
        TransformCompiler.TransformResult? Transformation = null,
        /// <summary>Non-null only when she starts at one of several places at random.</summary>
        RandomSpawnCompiler.RandomSpawnResult? RandomSpawn = null,
        /// <summary>Non-null only when she must be beaten before she can be recruited.</summary>
        EnemyToAllyCompiler.EnemyToAllyResult? EnemyToAlly = null,
        /// <summary>
        /// True when the combat style ended up as a copy INSIDE this plugin. Read from the
        /// finished record rather than from the request, because asking to clone can silently
        /// fall back to a reference when the source cannot be resolved — and the two cases need
        /// opposite answers about whether the source plugin is still a required master.
        /// </summary>
        bool CombatStyleCloned = false);

    /// <param name="profile">The follower to build.</param>
    /// <param name="location">
    /// Spawn point chosen from the location library. When null the follower falls back to the
    /// built-in Whiterun exterior spot (also the path unit tests use without a live game).
    /// </param>
    /// <param name="voiceTypeEditorId">
    /// EditorID of the chosen voice type, needed only when the profile has custom dialogue —
    /// it names the folder the game reads voice files from. Without it the lines are still
    /// compiled, but silent, and the build reports why.
    /// </param>
    public CompileResult Compile(FollowerProfile profile, SpawnLocation? location,
        ICombatStyleGetter? combatStyleToClone = null, ICellGetter? placementCellSource = null,
        string? voiceTypeEditorId = null, FormKey? vampireRace = null,
        IReadOnlyList<(SpawnLocation Location, ICellGetter? CellSource)>? alternateSpawns = null)
    {
        var modKey = ModKey.FromFileName(profile.PluginName);
        var mod = new SkyrimMod(modKey, VanillaForms.Release) { IsSmallMaster = true };
        var prefix = SanitizePrefix(profile.EditorIdPrefix ?? profile.Name);

        // --- Allocation order is FIXED for determinism: NPC, [CSTY], RELA, ACHR ---
        var npc = mod.Npcs.AddNew($"{prefix}NPC");
        ConfigureNpc(npc, profile, prefix, mod, combatStyleToClone);
        ApplyVampirism(npc, profile, vampireRace);

        // Optional and experimental; emits nothing at all unless explicitly enabled.
        var evolution = new EvolutionCompiler(log).Compile(mod, npc, prefix, profile.Evolution);
        var transformation = new TransformCompiler(log).Compile(npc, profile.Transformation);

        var relationship = mod.Relationships.AddNew($"{prefix}Rel");
        relationship.Parent.SetTo(npc.FormKey);
        relationship.Child.SetTo(VanillaForms.PlayerNpc);
        // Not cosmetic: mods that key dialogue to relationship rank (RDO chief among them) choose
        // different lines for a Friend than for a Confidant or a Rival.
        relationship.Rank = (Relationship.RankType)profile.Behavior.Relationship;
        relationship.AssociationType.Clear();

        // Relationships toward other people, in the order the user listed them so a rebuild is
        // byte-identical. Karla Raven ships these; they are what tie a follower into the world.
        var otherIndex = 0;
        foreach (var other in profile.Behavior.OtherRelationships)
        {
            var rela = mod.Relationships.AddNew($"{prefix}Rel{++otherIndex:D2}");
            rela.Parent.SetTo(npc.FormKey);
            rela.Child.SetTo(FormKey.Factory(other.Npc.FormKey));
            rela.Rank = (Relationship.RankType)other.Rank;
            rela.AssociationType.Clear();
        }

        var placedKey = BuildPlacement(mod, npc, profile, location, placementCellSource);

        // Enemy-to-Ally, if asked for: her hostile form, the tome she carries and the spell it
        // teaches. This also flips her own placed reference to start disabled.
        var allyRef = mod.EnumerateMajorRecords<PlacedNpc>().FirstOrDefault(r => r.FormKey == placedKey);
        var enemyToAlly = allyRef is not null
            ? new EnemyToAllyCompiler(log).Compile(mod, npc, allyRef, prefix, profile.Name, profile.EnemyToAlly)
            : null;

        // Spawn points. With Enemy-to-Ally the quest places the ENEMY; otherwise it moves her.
        var randomSpawn = alternateSpawns is { Count: > 0 }
            ? new RandomSpawnCompiler(log).Compile(mod, placedKey, prefix, alternateSpawns,
                enemyToAlly?.EnemyNpc)
            : null;

        // Dialogue is allocated last so adding lines never shifts the FormIDs of the records a
        // previously built follower already has — the same profile still compiles byte-identically.
        var dialogue = new DialogueCompiler(log).Compile(mod, npc.FormKey, prefix, profile.Dialogue,
            voiceTypeEditorId ?? "UnknownVoice", profile.PluginName);

        var masters = mod.ModHeader.MasterReferences.Select(m => m.Master.FileName.String).ToList();
        log.Information("Compiled follower {Name}: NPC {Npc}, ACHR {Achr}, masters [{Masters}]",
            profile.Name, npc.FormKey.ID.ToString("X6"), placedKey.ID.ToString("X6"),
            string.Join(", ", masters));

        // Whether the combat style is ours is a fact about the finished record, not about what
        // was asked for.
        var combatStyleCloned = !npc.CombatStyle.IsNull && npc.CombatStyle.FormKey.ModKey == modKey;

        return new CompileResult(mod, npc.FormKey, placedKey, npc.EditorID!, masters, dialogue,
            evolution, transformation, randomSpawn, enemyToAlly, combatStyleCloned);
    }

    /// <summary>
    /// Makes her a vampire the way the game itself does: swap the race for its vampire form and
    /// add the two keywords. No script, no abilities, no faction — a vanilla vampire NPC carries
    /// nothing else, so neither does she.
    ///
    /// The caller resolves the vampire race, because finding it needs the catalogue. Passing null
    /// leaves her mortal; the builder turns that into an error rather than shipping a follower
    /// who was meant to be a vampire and silently is not.
    /// </summary>
    private void ApplyVampirism(Npc npc, FollowerProfile profile, FormKey? vampireRace)
    {
        if (!profile.IsVampire || vampireRace is not { } race) return;

        npc.Race.SetTo(race);
        npc.Keywords ??= [];
        foreach (var keyword in new[] { VanillaForms.VampireKeyword, VanillaForms.ActorTypeUndeadKeyword })
            if (!npc.Keywords.Any(k => k.FormKey == keyword))
                npc.Keywords.Add(keyword.ToLink<IKeywordGetter>());

        log.Information("{Npc} is a vampire ({Race})", npc.EditorID, race);
    }

    /// <summary>
    /// Gives her a routine, referencing vanilla packages rather than authoring new ones — which
    /// is exactly what shipped followers do (Willow uses DefaultSandboxEditorLocation512, Laci
    /// uses …256), so this adds no dependency beyond Skyrim.esm.
    ///
    /// ORDER IS THE WHOLE GAME. Skyrim runs the first package whose conditions hold, so anything
    /// specific must sit above anything broad. Vanilla is unambiguous about this: Faendal lists
    /// nine timed eat/work/sleep packages and leaves the open-ended one last; the Whiterun
    /// housecarl ends with DefaultSandboxEditorLocation512 as her fallback. Put the sandbox first
    /// and the sleep package would simply never fire.
    /// </summary>
    private void AddPackages(Npc npc, FollowerProfile profile)
    {
        var behavior = profile.Behavior;

        // 1. Whatever the user chose deliberately — most specific, so highest priority.
        foreach (var extra in behavior.ExtraPackages)
            npc.Packages.Add(FormKey.Factory(extra.FormKey).ToLink<IPackageGetter>());

        // 2. Sleeping is time-bound, so it outranks the all-day sandbox below it.
        if (behavior.SleepsAtNight)
            npc.Packages.Add(VanillaForms.SleepEditorLoc24x8.ToLink<IPackageGetter>());

        // 3. The catch-all goes last.
        //
        // Dismissal matters here more than it looks. Vanilla's dismiss only does
        // AddToFaction/SetPlayerTeammate(false)/EvaluatePackage — there is no MoveTo — so where
        // she ends up is decided entirely by this package. An EDITOR-location sandbox sends her
        // back to where she was originally placed, which is wrong once she has been moved
        // somewhere else, so those followers settle wherever they actually are instead.
        var movedFromHerPlacement =
            profile.Placement.AlternateLocationIds.Count > 0 || profile.EnemyToAlly.IsUsable;
        var sandbox = behavior.Idle switch
        {
            _ when movedFromHerPlacement && behavior.Idle != IdleBehavior.EngineDefault
                => VanillaForms.SandboxCurrentLocation256,
            IdleBehavior.StaysPut => VanillaForms.SandboxEditorLocation256,
            IdleBehavior.WandersNearby => VanillaForms.SandboxEditorLocation512,
            IdleBehavior.SettlesWhereverSheIs => VanillaForms.SandboxCurrentLocation256,
            _ => FormKey.Null,
        };
        if (!sandbox.IsNull) npc.Packages.Add(sandbox.ToLink<IPackageGetter>());

        if (npc.Packages.Count > 0)
            log.Debug("{Npc}: {Count} AI package(s)", npc.EditorID, npc.Packages.Count);
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
                clone.FormVersion = 44;
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

        // Body: only pin a skin when asked. Left alone she inherits her race's default skin,
        // which is whatever body replacer the player has installed.
        if (profile.SkinArmor is { } skin)
            npc.WornArmor.SetTo(FormKey.Factory(skin.FormKey));

        // A legacy user-selected OTFT remains supported. In the equipment-first workflow the
        // compiler creates a private OTFT from the selected ARMO records because Skyrim uses
        // DefaultOutfit to decide what an NPC initially wears. The actual pieces also stay in
        // inventory so they can be traded normally.
        if (profile.Outfit is { } outfit)
            npc.DefaultOutfit.SetTo(FormKey.Factory(outfit.FormKey));
        else if (profile.EquippedArmor.Count > 0)
        {
            var generatedOutfit = mod.Outfits.AddNew($"{prefix}StartingEquipment");
            generatedOutfit.Items ??= [];
            foreach (var armor in profile.EquippedArmor)
                generatedOutfit.Items.Add(
                    FormKey.Factory(armor.FormKey).ToLink<IOutfitTargetGetter>());
            npc.DefaultOutfit.SetTo(generatedOutfit.FormKey);
        }
        // These three lists are null on a fresh NPC — they have to be created before anything
        // can be added, or handing her a weapon throws instead of arming her.
        if (profile.InventoryItems.Count > 0)
        {
            npc.Items ??= [];
            foreach (var item in profile.InventoryItems)
                npc.Items.Add(new ContainerEntry
                {
                    Item = new ContainerItem { Item = FormKey.Factory(item.FormKey).ToLink<IItemGetter>(), Count = 1 },
                });
        }

        if (profile.Spells.Count > 0)
        {
            npc.ActorEffect ??= [];
            foreach (var spell in profile.Spells)
                npc.ActorEffect.Add(FormKey.Factory(spell.FormKey).ToLink<ISpellRecordGetter>());
        }

        if (profile.Perks.Count > 0)
        {
            npc.Perks ??= [];
            foreach (var perk in profile.Perks)
                npc.Perks.Add(new PerkPlacement { Perk = FormKey.Factory(perk.FormKey).ToLink<IPerkGetter>(), Rank = 1 });
        }

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

        // Marriage is gated on this faction; without it the courtship dialogue never appears.
        if (profile.Marriageable)
            npc.Factions.Add(new RankPlacement
            {
                Faction = VanillaForms.PotentialMarriageFaction.ToLink<IFactionGetter>(),
                Rank = 0,
            });

        // AI values.
        npc.AIData ??= new AIData();
        npc.AIData.Aggression = (Aggression)Math.Clamp(profile.Ai.Aggression, (byte)0, (byte)3);
        npc.AIData.Confidence = (Confidence)Math.Clamp(profile.Ai.Confidence, (byte)0, (byte)4);
        npc.AIData.Assistance = (Assistance)Math.Clamp(profile.Ai.Assistance, (byte)0, (byte)2);
        npc.AIData.Mood = Mood.Neutral;
        npc.AIData.Responsibility = (Responsibility)Math.Clamp(profile.Ai.Morality, (byte)0, (byte)3);
        npc.AIData.EnergyLevel = profile.Ai.Energy;

        AddPackages(npc, profile);

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
        // Automatic calculation remains the normal path. Custom mode writes a complete DNAM
        // block and deliberately omits AutoCalcStats so the chosen values are authoritative.
        var flags = NpcConfiguration.Flag.Unique;
        if (profile.Stats.Mode == FollowerStatsMode.AutoCalculate)
        {
            flags |= NpcConfiguration.Flag.AutoCalcStats;
        }
        else
        {
            npc.PlayerSkills = new PlayerSkills
            {
                Health = profile.Stats.Health,
                Magicka = profile.Stats.Magicka,
                Stamina = profile.Stats.Stamina,
                Unused = 0,
                FarAwayModelDistance = 0,
                GearedUpWeapons = 0,
                Unused2 = new byte[3],
            };
            foreach (var (profileSkill, skyrimSkill) in FollowerSkillMap.All)
            {
                npc.PlayerSkills.SkillValues[skyrimSkill] = profile.Stats.GetSkill(profileSkill);
                npc.PlayerSkills.SkillOffsets[skyrimSkill] = 0;
            }

            // DNAM contains the exact primary values in custom mode; do not also apply ACBS
            // offsets or the in-game result would differ from what the editor displays.
            npc.Configuration.HealthOffset = 0;
            npc.Configuration.MagickaOffset = 0;
            npc.Configuration.StaminaOffset = 0;
        }
        if (profile.Female) flags |= NpcConfiguration.Flag.Female;
        if (profile.Protected) flags |= NpcConfiguration.Flag.Protected;
        if (profile.Essential) flags |= NpcConfiguration.Flag.Essential;
        npc.Configuration.Flags = flags;

        ApplyAppearance(npc, profile.Appearance, prefix, mod);
    }

    private static void ApplyAppearance(
        Npc npc, AppearanceSpec appearance, string prefix, SkyrimMod mod)
    {
        npc.Weight = Math.Clamp(appearance.Weight, 0f, 100f);

        if (appearance.HeadParts.Count > 0)
        {
            npc.HeadParts.Clear();
            foreach (var headPart in appearance.HeadParts)
                npc.HeadParts.Add(
                    FormKey.Factory(headPart.FormKey).ToLink<IHeadPartGetter>());
        }

        if (appearance.FaceMorphs.Count > 0)
        {
            npc.FaceMorph = new NpcFaceMorph();
            var morph = npc.FaceMorph;
            Action<float>[] setters =
            [
                value => morph.NoseLongVsShort = value,
                value => morph.NoseUpVsDown = value,
                value => morph.JawUpVsDown = value,
                value => morph.JawNarrowVsWide = value,
                value => morph.JawForwardVsBack = value,
                value => morph.CheeksUpVsDown = value,
                value => morph.CheeksForwardVsBack = value,
                value => morph.EyesUpVsDown = value,
                value => morph.EyesInVsOut = value,
                value => morph.BrowsUpVsDown = value,
                value => morph.BrowsInVsOut = value,
                value => morph.BrowsForwardVsBack = value,
                value => morph.LipsUpVsDown = value,
                value => morph.LipsInVsOut = value,
                value => morph.ChinNarrowVsWide = value,
                value => morph.ChinUpVsDown = value,
                value => morph.ChinUnderbiteVsOverbite = value,
                value => morph.EyesForwardVsBack = value,
            ];
            for (var i = 0; i < Math.Min(setters.Length, appearance.FaceMorphs.Count); i++)
            {
                var value = appearance.FaceMorphs[i];
                if (!float.IsNaN(value) && !float.IsInfinity(value) && value < float.MaxValue)
                    setters[i](value);
            }
        }

        if (appearance.FacePresets.Count > 0)
        {
            uint Value(int index) =>
                index < appearance.FacePresets.Count && appearance.FacePresets[index] != uint.MaxValue
                    ? appearance.FacePresets[index]
                    : 0;
            npc.FaceParts = new NpcFaceParts
            {
                Nose = Value(0),
                Unknown = Value(1),
                Eyes = Value(2),
                Mouth = Value(3),
            };
        }

        if (appearance.TintLayers.Count > 0)
        {
            npc.TintLayers.Clear();
            foreach (var tint in appearance.TintLayers)
            {
                var color = ArgbColor(tint.ArgbColor);
                npc.TintLayers.Add(new TintLayer
                {
                    Index = tint.Index,
                    Color = color,
                    // RaceMenu packs layer intensity into the ARGB alpha byte. TINV is a
                    // 0..1 interpolation value, not a boolean presence flag.
                    InterpolationValue = color.A / 255f,
                });
            }
        }

        if (appearance.HairColorArgb is { } hairArgb)
        {
            var colorRecord = mod.Colors.AddNew();
            colorRecord.EditorID = $"{prefix}HairColor";
            colorRecord.Color = ArgbColor(hairArgb);
            npc.HairColor.SetTo(colorRecord.FormKey);
        }

        npc.TextureLighting = TryParseRgba(appearance.SkinToneRgba, out var skinColor)
            ? skinColor
            : System.Drawing.Color.FromArgb(255, 255, 255, 255);
    }

    private static System.Drawing.Color ArgbColor(uint argb) =>
        System.Drawing.Color.FromArgb(
            (byte)(argb >> 24),
            (byte)(argb >> 16),
            (byte)(argb >> 8),
            (byte)argb);

    private static bool TryParseRgba(string? rgba, out System.Drawing.Color color)
    {
        color = default;
        if (rgba is null || rgba.Length != 8
            || !uint.TryParse(rgba, System.Globalization.NumberStyles.HexNumber,
                System.Globalization.CultureInfo.InvariantCulture, out var packed))
            return false;
        color = System.Drawing.Color.FromArgb(
            (byte)packed,
            (byte)(packed >> 24),
            (byte)(packed >> 16),
            (byte)(packed >> 8));
        return true;
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
        // TES4 major-record bit 0x00000400 is Persistent for ACHR/REFR. Mutagen's shared
        // enum intentionally has no record-specific name for this overloaded bit.
        placed.SkyrimMajorRecordFlags |=
            (SkyrimMajorRecord.SkyrimMajorRecordFlag)0x00000400;

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
