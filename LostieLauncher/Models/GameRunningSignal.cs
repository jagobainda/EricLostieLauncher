namespace LostieLauncher.Models;

/// <summary>
/// Whether the launcher believes a game is up, and on what evidence. The two positive signals carry
/// very different confidence, so they are kept apart instead of collapsed into a bool.
/// </summary>
public enum GameRunningSignal
{
    /// <summary>Nothing suggests the game is up.</summary>
    NotRunning,

    /// <summary>
    /// The launcher started the game itself and that process is still alive. This is conclusive, so
    /// an uninstall is refused outright.
    /// </summary>
    TrackedProcess,

    /// <summary>
    /// No process is tracked, but something holds <c>Game.exe</c> open. Usually that is the game,
    /// started outside this launcher session — but Explorer reading the icon, an antivirus scanning
    /// on access or a backup agent hold it too. Refusing on this alone would strand the user with a
    /// game that can never be uninstalled, which is the very failure being fixed, so it only warns.
    /// </summary>
    ExecutableLocked
}
