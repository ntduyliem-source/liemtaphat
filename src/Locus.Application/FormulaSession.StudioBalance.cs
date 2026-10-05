using Locus.Core;
using Locus.Core.Assistance;
using Locus.Core.Detection;

namespace Locus.Application;

public sealed partial class FormulaSession
{
    public bool StudioChemistryEnabled => State.Settings.Mode != InputMode.Markers &&
        State.Settings.EnabledDomains is DetectionDomains.All or DetectionDomains.Chemistry;
    public bool CanRunStudioBalance => StudioChemistryEnabled && !disposed && !IsBusy && !IsComposing && !IsBalancing;
    private IReadOnlyList<StudioChemistryOutcome> studioOutcomes = [];
    private ContentDocument? studioOutcomeOwner;
    public IReadOnlyList<StudioChemistryOutcome> StudioChemistryOutcomes => ReferenceEquals(studioOutcomeOwner,State.Content) ? studioOutcomes : [];
    public static bool HasManagedCoefficientChange(ContentRegion region)
    {
        if (region.ResultOverride is not { ManagedBalance: true } result) return false;
        var before = result.BalanceBefore?.Candidate ?? region.Selected;
        return before != null && result.Candidate is {} after && !SameCoefficients(before, after);
    }
    private static bool SameCoefficients(Candidate before, Candidate after) =>
        ChemistryProjection.Create(before.Document.Root).Source.Raw == ChemistryProjection.Create(after.Document.Root).Source.Raw;

    public bool HasStudioBalance(Guid? regionId = null) => State.Content?.Regions.Any(r =>
        (regionId == null || r.Id == regionId) && HasManagedCoefficientChange(r)) == true;

    public StudioChemistryCommand StudioCommand(StudioChemistryAction action, IEnumerable<Guid>? ids = null) =>
        new(Version, action, Array.AsReadOnly((ids ?? State.Content?.Regions.Select(r => r.Id) ?? []).Distinct().ToArray()));

    public Task<bool> StudioBalanceAsync(Guid? regionId = null, Func<Task<bool>>? validateInput = null) =>
        RunStudioChemistryAsync(StudioCommand(HasStudioBalance(regionId) ? StudioChemistryAction.CancelBalance : StudioChemistryAction.Balance,
            regionId is {} id ? [id] : null), validateInput);

    /// <summary>One explicit action: fill and balance all, or undo managed coefficients without removing products.</summary>
    public async Task<bool> RunStudioChemistryAsync(StudioChemistryCommand command, Func<Task<bool>>? validateInput = null)
    {
        if (!CanRunStudioBalance || command.Version != Version || State.Content is not {} content ||
            !Enum.IsDefined(command.Action) || command.RegionIds.Count > ContentDocument.RegionLimit ||
            command.RegionIds.Distinct().Count() != command.RegionIds.Count || command.RegionIds.Any(id => !content.Regions.Any(r => r.Id == id))) return false;
        var ids = command.RegionIds.ToHashSet();
        var regions = content.Regions.Where(r => ids.Contains(r.Id)).ToArray();
        bool cancel = command.Action == StudioChemistryAction.CancelBalance;
        DismissAssistance();
        var before = State;
        long version = Version, epoch = balanceEpoch;
        using var cancellation = new CancellationTokenSource();
        balancePending = cancellation; IsBalancing = true; studioOutcomes = []; BalanceChanged?.Invoke();
        try
        {
            var changes = new List<ContentChange>();
            var messages = new List<string>();
            var outcomes = new List<StudioChemistryOutcome>();
            int filled = 0, unchanged = 0, balanced = 0, already = 0;
            foreach (var region in regions)
            {
                await Task.Yield();
                if (!BalanceCurrent(before, version, epoch, cancellation.Token)) return false;
                ContentRegion? after = null;
                if (cancel)
                {
                    if (HasManagedCoefficientChange(region)) { after = CancelledRegion(region); Report("cancelled", "Đã khôi phục hệ số trước cân bằng; giữ sản phẩm."); }
                }
                else if (command.Action == StudioChemistryAction.DropProducts)
                {
                    if (region.ResultOverride?.ProductProposal != null && !HasManagedCoefficientChange(region)) { after = region with { ResultOverride = null, KeepFromAuto = true }; Report("products-removed", "Đã hủy tự điền; khôi phục nguồn phương trình."); }
                    else if (HasManagedCoefficientChange(region)) Report("cancel-balance-first", "Hủy cân bằng trước khi hủy tự điền.");
                }
                else if (!region.KeepText && !HasManagedCoefficientChange(region))
                {
                    if (region.Display?.Document.Root.Type == "ChemReaction")
                    {
                        var assessment = await AssessBalance(region, cancellation.Token);
                        if (assessment.Status == "Balanced") { after = BalancedRegion(region, assessment.Result!); balanced++; Report("balanced", assessment.Message); }
                        else if (assessment.Status == "AlreadyBalanced")
                        { already++; Report("already-balanced", "Phương trình đã cân bằng; giữ hệ số đã nhập."); }
                        else { unchanged++; messages.Add(assessment.Message); Report(assessment.Status, assessment.Message); }
                    }
                    else if (region.Display == null && ContentReactionDrafts.IsDraft(region.Raw, before.Settings, before.SourceRevision) && scheduler is IChemistryAssistanceScheduler executor)
                    {
                        var settings = before.Settings with { Mode = Locus.Core.Detection.InputMode.Explicit };
                        var request = new ChemistryAssistanceRequest(new(region.Raw, before.SourceRevision, settings), 0, [], UseUniqueCatalogConditions: true);
                        var reply = await executor.AssistAsync(request, cancellation.Token);
                        if (!BalanceCurrent(before, version, epoch, cancellation.Token)) return false;
                        if (reply.SourceId != new SourceSnapshot(region.Raw, before.SourceRevision).Id || reply.SourceRevision != before.SourceRevision)
                            throw new FormatException("Completion source mismatch.");
                        if (reply.Status == "available" && reply.Proposals.Length == 1)
                        {
                            var proposal = AssistanceSerializer.Deserialize(reply.Proposals[0]);
                            if (proposal.Kind != "complete-reaction" || proposal.Region.Source.Id != reply.SourceId ||
                                proposal.Context.Id != reply.ContextId || proposal.Context.SettingsFingerprint != AssistanceHistory.SettingsFingerprint(settings) ||
                                proposal.Region.Source.Slice(proposal.Region.ReplacementSpan) != region.Raw)
                                throw new FormatException("Completion context mismatch.");
                            if (!proposal.Region.IsClosed)
                            {
                                unchanged++; messages.Add("Đóng cặp bọc của phương trình còn dở trước khi điền sản phẩm."); Report("incomplete", messages[^1]); continue;
                            }
                            var draft = ReactionDraftParser.Parse(proposal.Region, cancellation.Token).Draft ?? throw new FormatException("Missing reaction draft.");
                            var products = ChemistryProjection.ProductsBeforeBalance(draft, proposal);
                            var productRegion = region with
                            {
                                Problem = null,
                                KeepFromAuto = false,
                                ResultOverride = new(region.SelectedId ?? "draft:" + region.Id, products, products.Candidates[0].Id,
                                    "product/" + proposal.Provenance.RuleId, ProductProposal: AssistanceSerializer.Serialize(proposal))
                            };
                            bool coefficientsChanged = !SameCoefficients(products.Candidates[0], proposal.Result.Candidates[0]);
                            after = coefficientsChanged ? BalancedRegion(productRegion, proposal.Result) : productRegion;
                            if (coefficientsChanged) balanced++;
                            filled++; messages.Add(reply.Message); Report("filled", reply.Message);
                        }
                        else { unchanged++; messages.Add(reply.Message); Report(reply.Status, reply.Message); }
                    }
                    else if (region.Display == null) Report("not-reaction", "Vùng này chưa phải phương trình Hóa rõ nghĩa; nguồn được giữ nguyên.");
                }
                if (after != null) changes.Add(new(region, after));
                void Report(string status, string message) => outcomes.Add(new(region.Id, status, message));
            }
            if (!BalanceCurrent(before, version, epoch, cancellation.Token)) return false;
            if (validateInput != null && !await validateInput()) return false;
            if (!BalanceCurrent(before, version, epoch, cancellation.Token) || !StudioChemistryEnabled) return false;
            balancePending = null; IsBalancing = false; cycle = []; quickBatch = null;
            bool committed = CommitContentChanges(before, changes, cancel ? "cancel-balance-all" : command.Action == StudioChemistryAction.DropProducts ? "drop-product" : "balance-all");
            studioOutcomes = outcomes.AsReadOnly(); studioOutcomeOwner = State.Content;
            BalanceMessage = command.Action == StudioChemistryAction.DropProducts ? changes.Count > 0 ? "Đã hủy tự điền; khôi phục nguồn phương trình." : "Không có sản phẩm do Locus tự điền trong phần này." : cancel
                ? $"Đã hủy cân bằng {changes.Count} phương trình; giữ nguyên sản phẩm đã điền."
                : balanced + filled + already == 0 ? "Chưa có phương trình có thể cân bằng hoặc tự điền trong phần này."
                : string.Join(" · ", new[] { balanced > 0 ? $"Đã cân bằng {balanced} phương trình" : null, filled > 0 ? $"Tự điền {filled} phương trình" : null, already > 0 ? $"{already} phương trình đã cân bằng sẵn" : null }.Where(s => s != null));
            if (unchanged > 0) BalanceMessage += $" {unchanged} phương trình giữ nguyên.";
            foreach (string message in messages.Where(m => !string.IsNullOrWhiteSpace(m)).Distinct().Take(2)) BalanceMessage += " " + message;
            BalanceChanged?.Invoke();
            return committed;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception)
        {
            if (BalanceCurrent(before, version, epoch, CancellationToken.None)) BalanceMessage = "Chưa xử lý xong; chưa thay đổi phương trình nào. Có thể bấm thử lại.";
            return false;
        }
        finally
        {
            if (ReferenceEquals(balancePending, cancellation))
            { balancePending = null; IsBalancing = false; BalanceChanged?.Invoke(); }
        }
    }
}
