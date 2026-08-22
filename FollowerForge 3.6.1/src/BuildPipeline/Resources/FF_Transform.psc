Scriptname FF_Transform extends Actor
{Follower Forge - optional, EXPERIMENTAL transformation.

 Turns the follower into something else when a fight starts, and back when it ends.

 Beast form uses Actor.SetRace() only. The vanilla "Beast Form" visual spell is deliberately
 NOT used: its effect waits ten seconds and SetRace()s the target on its own, which
 re-applies the beast race AFTER combat and AFTER Revert() has already run.

 Actor.psc documents SetRace() with no argument as restoring the original race:
   "akRace - OPTIONAL (Def=None) ... Default, no race, to switch back to the original race."
 We also store originalRace so revert does not depend on that alone.

 The transform is guarded by a flag because OnCombatStateChanged fires repeatedly.
 After the delay, combat is checked again so a short fight cannot change form after it ended.
 OnUpdate is a backup if the engine never delivers combat-state 0.
 OnLoad undoes a beast form left over from a save, because `transformed` is script state
 and resets on load.}

Race Property BeastRace Auto
{Race to become. Leave empty to only cast spells.}

Spell Property TransformFX Auto
{Optional visual the user chose. Do not point this at the vanilla beast-form visual spell.}

Spell Property OnTransform Auto
{Optional spell cast after transforming - a mod's own transformation, a summon, a buff.}

Bool Property RevertOutOfCombat = True Auto
{Change back once the fight is over.}

Float Property DelaySeconds = 2.0 Auto
{Beat before transforming, so it does not happen mid-swing.}

Bool transformed = false
Race originalRace = None

Event OnLoad()
    If RevertOutOfCombat && BeastRace != None && GetRace() == BeastRace
        RestoreRace()
        transformed = false
    EndIf
EndEvent

Event OnCombatStateChanged(Actor akTarget, Int aeCombatState)
    ; 0 not in combat, 1 in combat, 2 searching. Only 1 is a real fight.
    If aeCombatState == 1
        Transform()
    ElseIf aeCombatState == 0 && RevertOutOfCombat
        Revert()
    EndIf
EndEvent

Event OnUpdate()
    If transformed && RevertOutOfCombat && GetCombatState() == 0
        Revert()
    ElseIf transformed && RevertOutOfCombat
        RegisterForSingleUpdate(2.0)
    EndIf
EndEvent

Function Transform()
    If transformed
        Return
    EndIf
    transformed = true

    If DelaySeconds > 0.0
        Utility.Wait(DelaySeconds)
    EndIf

    ; Fight already over. Do not change form after the battle.
    If GetCombatState() == 0
        transformed = False
        Return
    EndIf

    If originalRace == None
        originalRace = GetRace()
    EndIf

    If TransformFX != None
        TransformFX.Cast(Self, Self)
    EndIf
    If BeastRace != None
        SetRace(BeastRace)
    EndIf
    If OnTransform != None
        OnTransform.Cast(Self, Self)
    EndIf

    If RevertOutOfCombat
        RegisterForSingleUpdate(2.0)
    EndIf
EndFunction

Function Revert()
    If !transformed && (BeastRace == None || GetRace() != BeastRace)
        Return
    EndIf
    transformed = false

    If TransformFX != None
        DispelSpell(TransformFX)
    EndIf
    RestoreRace()
EndFunction

Function RestoreRace()
    If originalRace != None
        SetRace(originalRace)
    Else
        SetRace()
    EndIf
EndFunction
