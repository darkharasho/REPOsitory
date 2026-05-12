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

## State sync verification (for Approach A)

**State field:** `MuseumPropMoneyHead.state` of type `State` (a public `enum State` defined in the same class with values Open, Closing, Closed, Opening, OpenCoolingDown, OpenEvilEyes, OpenDraggingInPlayer)

**How state is propagated to clients:**
- [x] Via PunRPC named `StateSetRPC` called by master after state assignment — `StateSet(State _newState)` checks `SemiFunc.IsMasterClient()` then calls `photonView.RPC("StateSetRPC", RpcTarget.All, (int)_newState)`. `StateSetRPC` is decorated `[PunRPC]`, is validated with `SemiFunc.MasterOnlyRPC(_info)` to reject spoofed RPCs, and sets `stateStart = true; state = (State)_newState; stateTimer = 0f` on every client including master.
- [ ] Via PhotonView observed serialization (`OnPhotonSerializeView` writes `currentState`)
- [ ] Direct master-only assignment, clients run a different code path
- [ ] Other

**Is `hurtCollider.SetActive(true)` called by all clients in StateClosed, or master-only?** ALL clients

The `stateStart` block in `StateClosed()` contains no `IsMasterClient` guard:

```csharp
private void StateClosed()
{
    if (stateStart)
    {
        stateStart = false;
        stateTimer = 0f;
        stateTimerMax = 3f;
        evilEyes.SetActive(value: false);
        grunkaMaterial.SetColor("_EmissionColor", Color.black);
        lowPassWalls.SetActive(value: true);
        spotLight.enabled = false;
        hurtCollider.SetActive(value: true);   // <-- ALL clients run this
    }
    // ... per-frame update continues
}
```

Every client runs `StateMachine()` → `StateClosed()` in their own `Update()`. When master sends `StateSetRPC(State.Closed)` to all clients, each client independently enters `StateClosed()` with `stateStart == true` (set by the RPC) and executes the full stateStart block, including `hurtCollider.SetActive(value: true)`. There is no master-only gate around the SetActive call.

**Verdict on Approach A:**
- [ ] ✅ Confirmed: if master skips `hurtCollider.SetActive(true)` in StateClosed (or deactivates it right after), clients will also not have the collider active for that cycle.
- [x] ⚠️ Conditional: the state syncs but the hurtCollider toggle is local to each client's StateClosed block — so master must broadcast its skip-decision via RPC.
- [ ] ❌ Broken: state is local-only, each client transitions through StateClosed independently. Need Approach B.

State IS synced — `StateSetRPC` propagates `State.Closed` to all clients and they all enter `StateClosed()`. However, since each client runs `hurtCollider.SetActive(true)` locally in their own stateStart block, master suppressing its own SetActive does NOT prevent remote clients from arming their local hurtCollider. The existing plan to patch `HurtCollider.PlayerHurt` (from Task 0.1 research) already handles this correctly: that patch runs on each client for their own player (`if (!_player.photonView.IsMine) return`), so intercepting `PlayerHurt` on every client is the right layer — no additional broadcast RPC needed for the skip decision.

**Where the win-roll postfix should live:**

The win-roll decision must be made ONCE per StateClosed entry, not per-frame. The correct target for a postfix that fires once-per-cycle is the state-transition entry point:

- `[HarmonyPatch(typeof(MuseumPropMoneyHead), "StateSetRPC")]` as a postfix, checking `(State)_newState == State.Closed && SemiFunc.IsMasterClient()` — this fires exactly once per closed-cycle on master.

Alternatively, patch `StateClosed()` itself with a postfix that checks `__instance.stateStart` BEFORE it's cleared (requires a prefix that snapshots the flag), but that is more complex. The `StateSetRPC` postfix is cleaner: it fires once per state transition, not once per frame, and is naturally master-only by convention (non-master clients also receive the RPC and execute StateSetRPC, so the `IsMasterClient()` guard inside the postfix is required).

Note: `stateStart` is a private field (`private bool stateStart`), so accessing it from a postfix requires `Traverse` or `AccessTools.Field`.

## Acceptance check

Confirmed all of the following:
- [x] The patch-target method runs on the local client at the exact moment damage would be applied
- [x] It is reachable via `[HarmonyPatch(typeof(HurtCollider), "PlayerHurt")]` with a
      parent-type guard (`GetComponentInParent<MuseumPropMoneyHead>() != null`)
- [x] Returning false suppresses damage WITHOUT canceling the suck-in (suck-in is driven
      by `MuseumPropMoneyHead`'s state machine in `Update()`, which runs independently)
- [x] `__instance` is a `MonoBehaviour` so `__instance.transform.position` works —
      confirmed because `HurtCollider : MonoBehaviour`

## Money-bag spawn API

**Valuable class:** `ValuableObject` — the single component present on every spawnable valuable prefab; holds `dollarValueOriginal`, `dollarValueCurrent`, and `dollarValueOverride` fields and drives value replication.

**Money-bag prefab reference:** `AssetManager.instance.surplusValuableSmall` (a `GameObject`) — accessed at runtime, no string constant required. The resource path passed to Photon is `"Valuables/" + AssetManager.instance.surplusValuableSmall.name` (e.g. `"Valuables/Surplus Valuable Small"`). A larger variant `AssetManager.instance.surplusValuableBig` exists (used when surplus > 10 000; `surplusValuableMedium` when surplus > 5 000). The `SurplusValuable` MonoBehaviour plays a coin burst and short-lived indestructibility on Start but is otherwise a normal valuable. There is no dedicated "MoneyBag" prefab; surplus valuables ARE the money-bag objects.

**Spawn entry point (host-only):** `PhotonNetwork.InstantiateRoomObject(string resourcePath, Vector3 position, Quaternion rotation, byte group)` — called directly (not wrapped in a dedicated method). The canonical pattern from `EnemySpinny.SpawnMoneyBag()` and `ExtractionPoint` is:

```csharp
GameObject prefab = AssetManager.instance.surplusValuableSmall;
GameObject spawned = SemiFunc.IsMultiplayer()
    ? PhotonNetwork.InstantiateRoomObject("Valuables/" + prefab.name, position, Quaternion.identity, 0)
    : Object.Instantiate(prefab, position, Quaternion.identity);
spawned.GetComponent<ValuableObject>().dollarValueOverride = value;
```

**Does the spawn API internally call PhotonNetwork.Instantiate / InstantiateRoomObject?** yes — `PhotonNetwork.InstantiateRoomObject` IS the spawn call. It is invoked directly by the host (no wrapper method). `InstantiateRoomObject` replicates the GameObject to all clients via Photon ownership transfer; no extra broadcast is needed.

**Master-only gate inside the spawn method?** no — `EnemySpinny.SpawnMoneyBag()` has no explicit `IsMasterClient` guard; the call site checks `moneyToBeSpawned && !isCollidingMoney` but does not gate on master. The broader `EnemySpinny` update loop is only run by the master (enemy AI is master-authoritative), which provides the implicit guard. Our `Payout.cs` must do its own `SemiFunc.IsMasterClientOrSingleplayer()` check before calling spawn — which Task 3.2's postfix already does.

**Per-instance dollar value:**
- Field/property: `ValuableObject.dollarValueOverride` (type `int`)
- Set before or after spawn? **after** — set immediately after `PhotonNetwork.InstantiateRoomObject` returns the `GameObject`, before the first frame runs on any client.
- Does the new value replicate to clients automatically? **yes, via RPC** — `ValuableObject.Start()` calls `StartCoroutine(DollarValueSet())`. That coroutine waits for `LevelGenerator` to finish and for the `PhotonView.ViewID` to be assigned, then on master calls `DollarValueSetLogic()` (which reads `dollarValueOverride` if non-zero and sets `dollarValueCurrent = dollarValueOverride`) and then fires `photonView.RPC("DollarValueSetRPC", RpcTarget.Others, dollarValueCurrent)` to push the resolved value to all other clients. `dollarValueOverride` must therefore be set **before the coroutine resolves** — i.e. in the same frame as the Instantiate call, which is guaranteed because the coroutine yields `WaitForSeconds(0.05f)` first.

**Code shape we'll use in Payout.cs (concrete, not pseudo):**

```csharp
// In Payout.Spawn(), called from our HurtCollider.PlayerHurt postfix on master only.
// position: __instance.transform.position (or offset upward)
// value: e.g. 50_000
public static void Spawn(Vector3 position, int value)
{
    if (!SemiFunc.IsMasterClientOrSingleplayer()) return;

    GameObject prefab = AssetManager.instance.surplusValuableSmall;
    GameObject spawned = SemiFunc.IsMultiplayer()
        ? PhotonNetwork.InstantiateRoomObject("Valuables/" + prefab.name, position, Quaternion.identity, 0)
        : UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);

    // dollarValueOverride is read by DollarValueSetLogic(), which the
    // DollarValueSet coroutine calls after a 0.05 s yield, so setting it
    // here (same frame as Instantiate) is always in time.
    spawned.GetComponent<ValuableObject>().dollarValueOverride = value;
}
```

**Risks / unknowns:**
- The prefab name (`AssetManager.instance.surplusValuableSmall.name`) must match the name of the prefab under `Assets/Resources/Valuables/`. In vanilla this is consistent, but if a future game update renames the asset the path will break. A safer fallback is `"Valuables/Surplus Valuable Small"` as a string constant if the name is confirmed stable.
- `dollarValueOverride` is type `int` but `dollarValueCurrent` / `dollarValueOriginal` are `float`. The cast is implicit and lossless for typical dollar amounts.
- `DollarValueSet()` also calls `RoundDirector.instance.haulGoalMax += (int)dollarValueCurrent` on master, so spawning a 50 000 surplus valuable will increase the displayed haul goal. This is cosmetically correct (more goal = more tension) but is a visible side-effect if many bags are spawned.
- The `SurplusValuable` component (on the small surplus prefab) marks the object briefly indestructible (`indestructibleTimer = 3f`) and plays coin particles on Start. This is desirable visual feedback for a win payout.
- `dollarValueOverride = 0` means "use the random range from the valuePreset" — so callers must pass a non-zero value to override.
