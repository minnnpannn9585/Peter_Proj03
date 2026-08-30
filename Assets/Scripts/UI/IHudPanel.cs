using UnityEngine;

namespace ParcelSort
{
    /// <summary>
    /// A HUD panel that builds its own subtree and exposes one root object for the phase-based
    /// show/hide pass. Having this as an interface means <see cref="HudRoot"/> can construct and
    /// toggle panels uniformly, so adding a panel cannot accidentally miss a visibility rule.
    /// </summary>
    public interface IHudPanel
    {
        /// <summary>Creates this panel's contents. Called once, before any Bind.</summary>
        void Build(Transform parent);

        /// <summary>Root object toggled by <see cref="HudRoot.Apply"/>.</summary>
        GameObject Panel { get; }

        /// <summary>Attaches the director this panel reads from.</summary>
        void Bind(YardDirector director);

        /// <summary>Re-reads state and updates the visuals.</summary>
        void Refresh();
    }
}
