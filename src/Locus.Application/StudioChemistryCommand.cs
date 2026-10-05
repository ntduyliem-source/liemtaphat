namespace Locus.Application;

public enum StudioChemistryAction { Balance, CancelBalance, DropProducts }
public sealed record StudioChemistryCommand(long Version, StudioChemistryAction Action, IReadOnlyList<Guid> RegionIds);
public sealed record StudioChemistryOutcome(Guid RegionId, string Status, string Message);
