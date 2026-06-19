using UnityEngine;

namespace ForcedFriendship
{
    /// <summary>
    /// Finds the main hauling cart in the level. A level may contain a small cart and the
    /// main cart (PhysGrabCart.isSmallCart distinguishes them); the main cart is preferred.
    /// Returns null when no cart exists yet. Callers should cache the result — this scans.
    /// </summary>
    internal static class CartLocator
    {
        internal static PhysGrabCart? FindMainCart()
        {
            PhysGrabCart[] carts = Object.FindObjectsOfType<PhysGrabCart>();
            PhysGrabCart? fallback = null;
            foreach (PhysGrabCart c in carts)
            {
                if (c == null) continue;
                if (!c.isSmallCart) return c;
                fallback ??= c;
            }
            return fallback;
        }
    }
}
