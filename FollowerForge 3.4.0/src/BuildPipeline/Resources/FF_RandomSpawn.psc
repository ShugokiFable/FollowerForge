Scriptname FF_RandomSpawn extends Quest
{Follower Forge - optional. She starts at one of several places, chosen at random.

 Modelled on how the Enemy-to-Ally mods do it: a start-game-enabled quest picks one of a few
 spots and puts the follower there, so she is somewhere different each playthrough.

 Two deliberate differences from those mods:
  - This is our own script under our own name. The E2A mods all ship an identical KWYK_Quest.pex,
    which is why they all report conflicts with each other; adding another copy would join that
    pile, and it would mean redistributing someone else's script.
  - They spawn a fresh copy of the actor with PlaceActorAtMe. This MOVES the follower who was
    already placed, so she keeps one persistent reference — which is what dialogue conditions,
    relationships and any framework tracking her rely on.

 If the script never runs she simply stays where Follower Forge placed her, which is a normal
 working follower and not a broken one.}

ObjectReference Property Follower Auto
{The follower's own placed reference. Moved to the chosen spot when SpawnBase is empty.}

ActorBase Property SpawnBase Auto
{Enemy-to-Ally mode: place a NEW actor of this base at the chosen spot instead of moving the
 follower. That is the hostile form the player has to find and beat; the ally stays disabled
 until she is summoned.}

ObjectReference Property Spot1 Auto
ObjectReference Property Spot2 Auto
ObjectReference Property Spot3 Auto
ObjectReference Property Spot4 Auto

Event OnInit()
    If Follower == None && SpawnBase == None
        Return
    EndIf

    ObjectReference[] spots = new ObjectReference[4]
    Int count = 0

    If Spot1 != None
        spots[count] = Spot1
        count += 1
    EndIf
    If Spot2 != None
        spots[count] = Spot2
        count += 1
    EndIf
    If Spot3 != None
        spots[count] = Spot3
        count += 1
    EndIf
    If Spot4 != None
        spots[count] = Spot4
        count += 1
    EndIf

    If count <= 0
        Return
    EndIf

    ; RandomInt is inclusive at both ends.
    ObjectReference chosen = spots[Utility.RandomInt(0, count - 1)]

    If SpawnBase != None
        ; Enemy-to-Ally: a fresh hostile actor waits there to be found.
        chosen.PlaceActorAtMe(SpawnBase)
    ElseIf Follower != None
        Follower.MoveTo(chosen)
    EndIf
EndEvent
