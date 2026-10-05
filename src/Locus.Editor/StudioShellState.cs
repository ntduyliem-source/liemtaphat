namespace Locus.Editor;

// Navigation guards and document sessions live here; each product composes its own chrome.
public sealed record StudioShellState(string Active, string Theme, bool CanUndo, bool CanRedo,
    Func<string, Task> Navigate, Func<bool, Task> History, Func<Task> ToggleTheme);
