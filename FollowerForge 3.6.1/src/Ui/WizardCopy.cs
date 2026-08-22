using FollowerForge.Domain;

namespace FollowerForge.Ui;

/// <summary>Every wizard sentence that names the follower. Sex changes refill these slots.</summary>
public static class WizardCopy
{
    public static string StepWho(FollowerPronouns p) => p.Fill("1   Who {subject} is");
    public static string StepLook(FollowerPronouns p) => p.Fill("2   {Possessive} look");
    public static string StepVoice(FollowerPronouns p) => p.Fill("3   {Possessive} voice");
    public static string StepFight(FollowerPronouns p) => p.Fill("4   How {subject} fights");
    public static string StepWear(FollowerPronouns p) => p.Fill("5   What {subject} wears");
    public static string StepWait(FollowerPronouns p) => p.Fill("6   Where {subject} waits");
    public static string StepBuild(FollowerPronouns p) => p.Fill("7   Build {object}");

    public static string WhoTitle(FollowerPronouns p) => p.Fill("Who is {subject}?");
    public static string ProtectedOption(FollowerPronouns p) =>
        p.Fill("Protected — only you can kill {object} (recommended)");
    public static string MortalOption(FollowerPronouns p) =>
        p.Fill("Mortal — {subject} can die permanently");
    public static string MarriageOption(FollowerPronouns p) =>
        p.Fill("{Subject} can be married (needs a voice that has marriage lines)");
    public static string RegardsYou(FollowerPronouns p) => p.Fill("How {subject} regards you");
    public static string KinHint(FollowerPronouns p) =>
        p.Fill("Give {object} history with people already in the world — a sister, a rival, an old friend. Each one makes the mod that NPC comes from a requirement.");
    public static string KinPeople(FollowerPronouns p) => p.Fill("{Possessive} people");

    public static string LookTitle(FollowerPronouns p) => p.Fill("What does {subject} look like?");
    public static string LookHint(FollowerPronouns p) =>
        p.Fill("Pick a face you exported from RaceMenu (Sculpt tab → F5 in game). Without one {subject} gets a plain default face.");
    public static string VampireOption(FollowerPronouns p) => p.Fill("{Subject} is a vampire");

    public static string VoiceTitle(FollowerPronouns p) => p.Fill("How does {subject} sound?");
    public static string VoicesUsable(FollowerPronouns p) => p.Fill("Voices {subject} can actually use");
    public static string CustomLinesHint(FollowerPronouns p) =>
        p.Fill("Write your own lines and FollowerForge speaks them in the voice you picked, with lip sync, so {subject} is not silent. These are {possessiveNoun} alone — they never change any other NPC.");
    public static string TalkToHer(FollowerPronouns p) => p.Fill("When you talk to {object}");
    public static string WhatSheSays(FollowerPronouns p) => p.Fill("what {subject} says…");
    public static string HerLines(FollowerPronouns p) => p.Fill("{Possessive} lines");

    public static string FightTitle(FollowerPronouns p) => p.Fill("How does {subject} fight?");
    public static string CloneStyle(FollowerPronouns p) =>
        p.Fill("Copy it into {possessive} plugin so I can tweak it later (never edits the original)");
    public static string AverageTemper(FollowerPronouns p) => p.Fill("Average - stands {possessive} ground");
    public static string TemperHint(FollowerPronouns p) =>
        p.Fill("These are the game's own five confidence ranks. Cowardly makes {object} run from danger — pick it if you want someone who has to grow into the job.");
    public static string EvolveTitle(FollowerPronouns p) => p.Fill("EXPERIMENTAL — let {object} grow");
    public static string EvolveHint(FollowerPronouns p) =>
        p.Fill("{Subject} starts timid and gains confidence, skills and health as {subject} survives fights beside you — the idea behind Melana the War Maiden. This is the ONLY feature that puts a script in your follower, and scripts stay in your save file in a way records do not. It has not been confirmed working in game. Test on a save you can throw away.");
    public static string EvolveOption(FollowerPronouns p) =>
        p.Fill("Let {object} evolve as {subject} fights (experimental)");
    public static string TransformHint(FollowerPronouns p) =>
        p.Fill("{Subject} transforms when a fight starts and changes back when it ends. Werewolf uses the game's own beast race and change effect and needs nothing installed. Custom uses a race and/or spell from your own mods — the same trick the transforming followers use. Also adds a script.");

    public static string WearTitle(FollowerPronouns p) => p.Fill("What does {subject} wear and carry?");
    public static string WearHint(FollowerPronouns p) =>
        p.Fill("Pick the actual armor, accessories, weapons, spells, and perks {subject} owns. Identical names show their FormID so you can tell variants apart. Biped-slot conflicts are checked before a plugin can be published.");
    public static string AmmoHint(FollowerPronouns p) =>
        p.Fill("A bow without arrows is a stick. Pick {possessive} ammo and how many {subject} carries — it goes into {possessive} inventory, which is how vanilla archers never run out.");
    public static string LoreHint(FollowerPronouns p) =>
        p.Fill("Things {subject} carries that say who {subject} is — a journal, a letter from home, a keepsake, {possessive} favourite mead. These go into {possessive} inventory, so you can take them from {object} or read them after recruiting {object}.");
    public static string SpellsLabel(FollowerPronouns p) => p.Fill("Spells {subject} knows");
    public static string PerksLabel(FollowerPronouns p) => p.Fill("Perks {subject} has");
    public static string BodyHint(FollowerPronouns p) =>
        p.Fill("Leave this empty and {subject} uses whatever body the player has installed — that is what OBody needs, and what almost every follower should do. Pick a skin only to pin {object} to one specific body, which makes that mod a hard requirement.");

    public static string PlaceTitle(FollowerPronouns p) => p.Fill("Where will {subject} be waiting?");
    public static string IdleLabel(FollowerPronouns p) => p.Fill("What {subject} does while {subject} waits");
    public static string IdleDefault(FollowerPronouns p) =>
        p.Fill("Leave it to the game ({subject} will still sit and eat)");
    public static string IdleSpot(FollowerPronouns p) => p.Fill("Keeps to {possessive} spot — barely strays");
    public static string IdleRoom(FollowerPronouns p) => p.Fill("Uses the room {subject} is in (recommended)");
    public static string IdleWherever(FollowerPronouns p) =>
        p.Fill("Settles wherever {subject} happens to be");
    public static string AlternateLabel(FollowerPronouns p) =>
        p.Fill("Or let {object} start somewhere different each game (optional)");
    public static string AlternateHint(FollowerPronouns p) =>
        p.Fill("Add up to four places from the list above. {Subject} then starts at one of them at random, the way the Enemy-to-Ally followers do — using our own script, so it does not conflict with theirs.");
    public static string E2AHint(FollowerPronouns p) =>
        p.Fill("Instead of waiting to be recruited, a hostile version of {object} lurks at one of the places above. Beat {object}, loot the spell tome {subject} carries, read it, and the spell summons {object} to you as a follower. {Subject} does not exist in the world until then.");
    public static string E2AOption(FollowerPronouns p) =>
        p.Fill("{Subject} has to be defeated before {subject} can be recruited");

    public static string BuildTitle(FollowerPronouns p) => p.Fill("Build {object}");
    public static string AssetsLabel(FollowerPronouns p) => p.Fill("{Possessive} appearance assets");
    public static string CopyAssets(FollowerPronouns p) => p.Fill("Copy {possessive} assets into my own asset hub");

    public static string NeedsName(FollowerPronouns p) => p.Fill("Give {object} a name first.");
    public static string SheNeedsName(FollowerPronouns p) => p.Fill("{Subject} needs a name (step 1).");
    public static string TypeLineFirst(FollowerPronouns p) => p.Fill("Type what {subject} says first.");
    public static string AlreadyKnows(FollowerPronouns p, string name) =>
        p.Fill($"{{Subject}} already has a relationship with {name}.");
    public static string FourPlaces(FollowerPronouns p) =>
        p.Fill("Four places is the most {subject} can choose between.");
    public static string LipMissing(FollowerPronouns p) =>
        p.Fill("xVASynth is installed but its lip_fuz plugin is missing — lines could be spoken but {possessive} mouth would not move. Enable lip_fuz in xVASynth.");
    public static string VoicesSheCanUse(FollowerPronouns p, int usable, int hidden) =>
        p.Fill($"{usable:N0} voices {{subject}} can use  ·  {hidden:N0} creature and unique voices hidden");
    public static string SilentVoice(FollowerPronouns p, string source) =>
        p.Fill($"{source} — no follower dialogue, {{subject}} would be silent");
    public static string DoneReady(FollowerPronouns p) => p.Fill("DONE — {subject} is ready to install.");
    public static string ShareHer(FollowerPronouns p) => p.Fill("If you share {object}:");
    public static string FindHer(FollowerPronouns p) =>
        p.Fill("Install the folder (or zip) with Vortex or MO2, then find {object} at the place you chose.");
    public static string FolderGone(FollowerPronouns p) => p.Fill("That folder is gone — build {object} again.");
    public static string KinTreats(FollowerPronouns p, string rank) =>
        p.Fill($"{{subject}} treats them as {rank}");
    public static string KinRank(FollowerPronouns p, string name, string rank) =>
        p.Fill($"{name}   —   {{possessive}} {rank}");
}
