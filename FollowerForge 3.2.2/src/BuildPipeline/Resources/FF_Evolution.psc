Scriptname FF_Evolution extends Actor
{Follower Forge - optional, EXPERIMENTAL evolution.

 Counts the fights she comes through beside you and steps her through phases,
 raising confidence and combat skills at each step.

 Design notes, because the failure modes here are silent and land in people's saves:
  - Every phase is applied from the RECORD's base values, never by adding to the current
    ones. Applying the same phase twice therefore changes nothing, so a reload, a re-entry
    into the cell, or a duplicated event cannot stack bonuses forever.
  - Phase and progress live in globals rather than in script state. That makes them visible
    to dialogue conditions and to the console, and it means a broken script loses the
    counters but never corrupts anything else.
  - Nothing here removes or replaces records. If this script never runs, she is simply a
    normal follower at her starting values.}

; ---- filled in by Follower Forge; do not rename, saves bind to these ----

GlobalVariable Property FF_Phase Auto
{Current phase, 1..MaxPhase. Public so dialogue can condition on it.}

GlobalVariable Property FF_Progress Auto
{Fights survived so far within the current phase.}

Int Property CombatsPerPhase = 25 Auto
{Fights needed to move up one phase.}

Int Property MaxPhase = 3 Auto

Int Property StartConfidence = 0 Auto
{0 cowardly .. 4 foolhardy, at phase 1.}

Int Property EndConfidence = 4 Auto
{Confidence once she reaches the final phase.}

Int Property SkillPerPhase = 15 Auto
{Added to her combat skills for each phase beyond the first.}

Float Property HealthPerPhase = 30.0 Auto
Float Property StaminaPerPhase = 20.0 Auto
Float Property MagickaPerPhase = 10.0 Auto

Bool wasFighting = false

Event OnInit()
    ApplyPhase()
EndEvent

Event OnCombatStateChanged(Actor akTarget, Int aeCombatState)
    If aeCombatState == 0
        ; Left combat. Only counts if she was actually in a fight, because this event also
        ; fires on searching/lost-target transitions.
        If wasFighting
            wasFighting = false
            RecordFight()
        EndIf
    Else
        wasFighting = true
    EndIf
EndEvent

Function RecordFight()
    If FF_Phase == None || FF_Progress == None
        Return
    EndIf
    If FF_Phase.GetValueInt() >= MaxPhase
        Return
    EndIf

    FF_Progress.SetValueInt(FF_Progress.GetValueInt() + 1)
    If FF_Progress.GetValueInt() >= CombatsPerPhase
        FF_Progress.SetValueInt(0)
        FF_Phase.SetValueInt(FF_Phase.GetValueInt() + 1)
        ApplyPhase()
    EndIf
EndFunction

Function ApplyPhase()
    If FF_Phase == None
        Return
    EndIf

    Int phase = FF_Phase.GetValueInt()
    If phase < 1
        phase = 1
        FF_Phase.SetValueInt(1)
    EndIf
    If phase > MaxPhase
        phase = MaxPhase
    EndIf

    ; Confidence walks from start to end across the phases.
    Int conf = StartConfidence
    If MaxPhase > 1
        conf = StartConfidence + ((EndConfidence - StartConfidence) * (phase - 1)) / (MaxPhase - 1)
    EndIf
    SetActorValue("Confidence", conf as Float)

    ; Everything below is computed from the base value, so this is safe to run repeatedly.
    Int steps = phase - 1
    Float skillBonus = (SkillPerPhase * steps) as Float

    SetActorValue("OneHanded", GetBaseActorValue("OneHanded") + skillBonus)
    SetActorValue("TwoHanded", GetBaseActorValue("TwoHanded") + skillBonus)
    SetActorValue("Marksman", GetBaseActorValue("Marksman") + skillBonus)
    SetActorValue("Block", GetBaseActorValue("Block") + skillBonus)
    SetActorValue("LightArmor", GetBaseActorValue("LightArmor") + skillBonus)
    SetActorValue("HeavyArmor", GetBaseActorValue("HeavyArmor") + skillBonus)
    SetActorValue("Restoration", GetBaseActorValue("Restoration") + skillBonus)

    SetActorValue("Health", GetBaseActorValue("Health") + (HealthPerPhase * steps))
    SetActorValue("Stamina", GetBaseActorValue("Stamina") + (StaminaPerPhase * steps))
    SetActorValue("Magicka", GetBaseActorValue("Magicka") + (MagickaPerPhase * steps))
EndFunction
