using System.Collections.Generic;
using UnityEngine;

namespace ForcedFriendship
{
    /// <summary>
    /// Finds the hauling carts in the level. A level may contain a small cart plus one or more
    /// main ("medium") carts (PhysGrabCart.isSmallCart distinguishes them). Players in Cart mode
    /// anchor to the nearest main cart, so this returns every non-small cart. Callers should
    /// cache the result — FindObjectsOfType scans the scene.
    /// </summary>
    internal static class CartLocator
    {
        /// <summary>
        /// Fills <paramref name="buffer"/> with the current main (non-small) carts and returns it.
        /// If no main cart exists but a small cart does, the small cart is used as a fallback so
        /// Cart mode still has an anchor.
        /// </summary>
        internal static List<PhysGrabCart> FindMainCarts(List<PhysGrabCart> buffer)
        {
            buffer.Clear();
            PhysGrabCart[] carts = Object.FindObjectsOfType<PhysGrabCart>();
            PhysGrabCart? smallFallback = null;
            foreach (PhysGrabCart c in carts)
            {
                if (c == null) continue;
                if (!c.isSmallCart) buffer.Add(c);
                else smallFallback ??= c;
            }
            if (buffer.Count == 0 && smallFallback != null) buffer.Add(smallFallback);
            return buffer;
        }
    }
}
