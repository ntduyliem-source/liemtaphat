using Locus.Application;

namespace Locus.Editor.Formula.Presentation;

public enum ResultSelectionKind { None, Region, Range, All }
public sealed record ResultSelectionState(ResultSelectionKind Kind, ContentSelection? Range)
{
    public static ResultSelectionState None { get; } = new(ResultSelectionKind.None, null);
}
