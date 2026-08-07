Scriptname FF_Transform extends Actor
{Follower Forge - optional, EXPERIMENTAL transformation.

 Turns her into something else when a fight starts, and back again when it ends.

 Covers two things with one mechanism:
  - Werewolf (and any other beast form): BeastRace is set, and Skyrim's own
    Actor.SetRace() with no argument restores her original race afterwards. That revert is
    documented in Actor.psc, not guessed:
      "akRace - OPTIONAL (Def=None) ... Default, no race, to switch back to the original race."
  - Mod-supplied transformations: OnTransform casts an installed spell instead of, or as well
    as, the race swap. This is exactly what the transforming followers do (Sirina Celone ships
    mihail_general_attachSPELL, which waits a few seconds after combat starts and casts).

 Notes on the failure modes, which are the point of writing this down:
  - Nothing here is authored by us. BeastRace and the spells are records the user already has,
    so a missing one leaves her an ordinary follower rather than a broken one.
  - The transform is guarded by a flag, because OnCombatStateChanged fires repeatedly during a
    fight and a race swap on every tick would be a disaster.
  - A beast race cannot wear or wield her equipment. That is Skyrim's behaviour, not a bug here;
    reverting restores it.}

Race Property BeastRace Auto
{Race to become - e.g. WerewolfBeastRace. Leave empty to only cast spells.}

Spell Property TransformFX Auto
{Optional visual, e.g. WerewolfChangeFX. Cast before the race swap so it reads as the change.}

Spell Property OnTransform Auto
{Optional spell cast after transforming - a mod's own transformation, a summon, a buff.}

Bool Property RevertOutOfCombat = True Auto
{Change back once the fight is over.}

Float Property DelaySeconds = 2.0 Auto
{Beat before transforming, so it does not happen mid-swing.}

Bool transformed = false

Event OnCombatStateChanged(Actor akTarget, Int aeCombatState)
    If aeCombatState != 0
        Transform()
    ElseIf RevertOutOfCombat
        Revert()
    EndIf
EndEvent

Function Transform()
    ; The guard matters: this event fires again on every target change during a fight.
    If transformed
        Return
    EndIf
    transformed = true

    If DelaySeconds > 0.0
        Utility.Wait(DelaySeconds)
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
EndFunction

Function Revert()
    If !transformed
        Return
    EndIf
    transformed = false

    If BeastRace != None
        ; No argument restores whatever race she was built with.
        SetRace()
    EndIf
EndFunction
