# MuseumGambling Research Notes

## Patch target on MuseumPropMoneyHead

**Method to patch:** `HurtCollider.PlayerHurt`

> Note: The damage does NOT live in `MuseumPropMoneyHead` itself. `MuseumPropMoneyHead`
> owns a child `hurtCollider` GameObject with a `HurtCollider` component. In `StateClosed()`
> (the stateStart block), it calls `hurtCollider.SetActive(value: true)`, which fires
> `HurtCollider.OnEnable()` → starts the `ColliderCheck()` coroutine → calls `PlayerHurt()`
> when a player is detected. The patch belongs on `HurtCollider.PlayerHurt`, filtered to
> instances whose parent is `MuseumPropMoneyHead`.

**Signature:** `private void PlayerHurt(PlayerAvatar _player)`

**Is it a `[PunRPC]`?** no — `PlayerHurt` is a plain private method; it guards itself with
`if (GameManager.Multiplayer() && !_player.photonView.IsMine) return;` so it only executes
meaningful work on the owning client, not via Photon RPC.

**How the clicking PlayerAvatar is reachable from the prefix:**
- [x] via `__args[0]` (parameter index 0 is `PlayerAvatar _player`)

**Damage call site (verbatim from decompilation — the `playerKill` branch, which is the
default and is what the Museum Head uses):**

```csharp
if (playerKill)
{
    onImpactAny.Invoke();
    onImpactPlayer.Invoke();
    num = health;
    _player.playerHealth.Hurt(_player.playerHealth.health, savingGrace: true, enemyIndex);
    flag3 = true;
}
```

(HurtCollider.cs lines 697–704 in the decompiled output)

**Does returning `false` from a Harmony prefix on this method suppress ONLY the damage
(not the suck-in animation)?** yes — The suck-in animation is entirely driven by
`MuseumPropMoneyHead`'s state machine (`StateOpenEvilEyes` → `StateOpenDraggingInPlayer`
→ `StateClosing` → `StateClosed`). All of that runs in `Update()`/`FixedUpdate()` via
`StateMachine()` independently of `HurtCollider.PlayerHurt`. By the time `PlayerHurt` is
called, the head is already fully closed and the suck-in is over. Returning `false` skips
only the damage + force application inside `PlayerHurt`; the `StateClosed` shaking, the
`hurtCollider` overlap loop, and the eventual `StateOpening` transition all continue
unaffected.

## Grab/click entry point (context only)

**Entry method:** `MuseumPropMoneyHead.DragInPlayerStart` — called externally (presumably
by a Unity event or the `PhysGrabObjectGrabArea` component) when a player grabs or clicks
the prop; it reads the latest grabber via `grabArea.GetLatestGrabber()`, stores them in
`playerToDragIn`, and transitions to `State.OpenEvilEyes` on the master client.

## Architecture notes

- `StateSet(State)` propagates state changes in multiplayer via
  `photonView.RPC("StateSetRPC", RpcTarget.All, ...)` — `StateSetRPC` IS a `[PunRPC]` —
  but it only controls state transitions, not damage.
- In `StateClosed()` (the `stateStart` block), `hurtCollider.SetActive(value: true)` arms
  the `HurtCollider`. `HurtCollider.OnEnable` immediately starts `ColliderCheck()`.
- `ColliderCheck()` runs every 0.05 s, detects the player in the collider, and calls
  `PlayerHurt(playerAvatar)`. Because `playerKill = true`, it calls
  `_player.playerHealth.Hurt(_player.playerHealth.health, ...)` — full-health kill — so
  it fires once (the player dies, no second hit lands within the 3 s `StateClosed` window).
- `PlayerHurt` is `private` but Harmony patches private methods fine via
  `AccessTools.Method(typeof(HurtCollider), "PlayerHurt")`.
- The `MuseumPropMoneyHead` parent is reachable from the prefix as
  `__instance.GetComponentInParent<MuseumPropMoneyHead>()` — use this to guard the prefix
  so it only fires for the Museum Head and not every `HurtCollider` in the level.
- Spawn position for the money bag: `__instance.transform.position` (the `HurtCollider`
  GameObject) or `__instance.GetComponentInParent<MuseumPropMoneyHead>().transform.position`.

## Acceptance check

Confirmed all of the following:
- [x] The patch-target method runs on the local client at the exact moment damage would be applied
- [x] It is reachable via `[HarmonyPatch(typeof(HurtCollider), "PlayerHurt")]` with a
      parent-type guard (`GetComponentInParent<MuseumPropMoneyHead>() != null`)
- [x] Returning false suppresses damage WITHOUT canceling the suck-in (suck-in is driven
      by `MuseumPropMoneyHead`'s state machine in `Update()`, which runs independently)
- [x] `__instance` is a `MonoBehaviour` so `__instance.transform.position` works —
      confirmed because `HurtCollider : MonoBehaviour`
