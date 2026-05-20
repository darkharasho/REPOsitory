using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;

namespace Overstaffed.Patches
{
    // Patch 3 (nice-to-have): the public/random matchmaking branch in
    // NetworkConnect.OnConnectedToMaster builds expectedMaxPlayers from a hardcoded
    // (byte)(matchmakingMaxPlayers ? 6u : 0u). The literal 6 is what restricts
    // PhotonNetwork.JoinRandomRoom / JoinRandomOrCreateRoom to vanilla-sized rooms.
    //
    // Replace the ldc.i4.6 with a call to GetExpectedMaxPlayers(), which returns our
    // configured cap. The conv.u1 immediately following keeps converting to byte.
    [HarmonyPatch(typeof(NetworkConnect), "OnConnectedToMaster")]
    internal static class NetworkConnectOnConnectedToMasterPatch
    {
        // Returns int (not byte) — the surrounding IL already does conv.u1 after this value.
        // Clamped to [1, 255] so we never overflow when the conv.u1 runs.
        public static int GetExpectedMaxPlayers()
        {
            int v = Plugin.ConfigMaxPlayers.Value;
            if (v < 1) return 1;
            if (v > 255) return 255;
            return v;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var helper = AccessTools.Method(
                typeof(NetworkConnectOnConnectedToMasterPatch),
                nameof(GetExpectedMaxPlayers));

            int replaced = 0;
            foreach (var instr in instructions)
            {
                // ldc.i4.6 is the short-form one-byte opcode used by the C# compiler for the
                // literal 6 in `matchmakingMaxPlayers ? 6u : 0u`. There is exactly one of these
                // in the method body (verified against 0.4.0 Assembly-CSharp.dll); the other
                // numeric literals in the method are 0, which uses ldc.i4.0.
                if (instr.opcode == OpCodes.Ldc_I4_6 && replaced == 0)
                {
                    replaced++;
                    yield return new CodeInstruction(OpCodes.Call, helper);
                    continue;
                }
                yield return instr;
            }

            if (replaced == 0)
                Plugin.Log.LogWarning(
                    "[NetworkConnect.OnConnectedToMaster] transpiler did NOT find ldc.i4.6 — " +
                    "public matchmaking expectedMaxPlayers will stay at vanilla 6. " +
                    "Private/friends lobbies still respect MaxPlayers via the GameManager patches.");
            else
                Plugin.Log.LogInfo("[NetworkConnect.OnConnectedToMaster] transpiler replaced ldc.i4.6 with GetExpectedMaxPlayers()");
        }
    }
}
