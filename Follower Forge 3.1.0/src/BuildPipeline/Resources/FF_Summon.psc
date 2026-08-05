Scriptname FF_Summon extends ActiveMagicEffect
{Follower Forge - summons the ally form of an Enemy-to-Ally follower.

 The ally is placed in the world but starts disabled, so she does not exist until you have
 beaten her hostile form and read the spell tome she was carrying. Casting the spell enables
 that reference and brings it to the player.

 Modelled on how the Enemy-to-Ally mods do it (their KWYK_Teleport), written fresh under our own
 name — theirs is shipped identically by every E2A mod, which is why they all conflict with one
 another.

 She is a persistent reference that already exists, so enabling her keeps one identity: her
 relationships, dialogue conditions and anything else tracking her all still apply.}

ObjectReference Property AllyToSummon Auto
{The follower's own placed reference, which starts disabled.}

Actor Property PlayerRef Auto
{Auto-filled with the player.}

Float Property Distance = 200.0 Auto
{How far in front of the player she appears.}

Event OnEffectStart(Actor akTarget, Actor akCaster)
    If AllyToSummon == None || PlayerRef == None
        Return
    EndIf

    AllyToSummon.Enable()
    Utility.Wait(0.2)

    ; Face the player, then stand in front of them.
    Float az = PlayerRef.GetAngleZ()
    AllyToSummon.SetAngle(0.0, 0.0, az + 180.0)
    AllyToSummon.MoveTo(PlayerRef as ObjectReference, \
        Distance * Math.Sin(az), Distance * Math.Cos(az), 0.0, false)
EndEvent
