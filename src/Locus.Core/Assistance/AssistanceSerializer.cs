using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Locus.Core.Detection;
using Locus.Core.Serialization;

namespace Locus.Core.Assistance
{
    public static class AssistanceSerializer
    {
        public const int MaxCharacters = 2_000_000;
        public static string Serialize(AssistanceProposal proposal) => Pack("proposal", new ProposalDto
        {
            Id = proposal.Id, Kind = proposal.Kind, Region = Region(proposal.Region), Settings = proposal.Context.SettingsFingerprint,
            Conditions = proposal.Context.Conditions.Select(f => new FactDto { Key = f.Key, Value = f.Value }).ToArray(),
            Result = CandidateSetSerializer.Serialize(proposal.Result), Original = proposal.OriginalReading == null ? null : CandidateSetSerializer.Serialize(proposal.OriginalReading),
            Solver = proposal.Provenance.SolverVersion, Catalog = proposal.Provenance.CatalogVersion, Rule = proposal.Provenance.RuleId, References = proposal.Provenance.References.ToArray()
        });
        public static AssistanceProposal Deserialize(string json) => Guard(() =>
        {
            var d = Unpack<ProposalDto>(json, "proposal");
            if (d.Conditions == null || d.Conditions.Length > 16 || d.Conditions.Any(f => f == null) || d.References == null) throw new FormatException("Missing proposal data.");
            var proposal = new AssistanceProposal(d.Kind!, Region(d.Region!), new AssistanceContext(d.Settings!, d.Conditions.Select(f => new ConditionFact(f.Key!, f.Value!))),
                CandidateSetSerializer.Deserialize(d.Result!), new AssistanceProvenance(d.Solver!, d.Catalog, d.Rule, d.References), d.Original == null ? null : CandidateSetSerializer.Deserialize(d.Original));
            if (proposal.Id != d.Id) throw new FormatException("Proposal identity mismatch.");
            return proposal;
        });
        public static string SerializeDraft(ReactionDraft draft) => Pack("draft", new DraftDto
        { Id = draft.Id, Region = Region(draft.Region), Reactants = CandidateSetSerializer.Serialize(draft.Reactants), SeparatorStart = draft.SeparatorSpan.Start, SeparatorEnd = draft.SeparatorSpan.End, Arrow = draft.Arrow });
        public static ReactionDraft DeserializeDraft(string json) => Guard(() =>
        {
            var d = Unpack<DraftDto>(json, "draft");
            var draft = new ReactionDraft(Region(d.Region!), CandidateSetSerializer.Deserialize(d.Reactants!), new TextSpan(d.SeparatorStart, d.SeparatorEnd), d.Arrow!);
            if (draft.Id != d.Id) throw new FormatException("Draft identity mismatch."); return draft;
        });
        private static T Guard<T>(Func<T> run)
        {
            try { return run(); }
            catch (Exception e) when (e is ArgumentException || e is SerializationException || e is XmlException || e is InvalidOperationException || e is OverflowException)
            { throw new FormatException("Invalid or unsupported assistance snapshot.", e); }
        }
        private static RegionDto Region(AssistanceRegion r) => new RegionDto
        { Raw = r.Source.Raw, Revision = r.Source.Revision, SourceId = r.Source.Id, Start = r.ContentSpan.Start, End = r.ContentSpan.End, ReplaceStart = r.ReplacementSpan.Start, ReplaceEnd = r.ReplacementSpan.End,
            Open = r.Markers?.Open, Close = r.Markers?.Close, Profile = r.Intent?.ProfileId, Domain = r.Intent?.Domain, RoutingVersion = r.Intent?.GrammarVersion, Closed = r.IsClosed };
        private static AssistanceRegion Region(RegionDto d)
        {
            if (d == null || d.Raw == null || (d.Open == null) != (d.Close == null) || (d.Profile == null) != (d.Domain == null) ||
                (d.Profile == null ? d.RoutingVersion != null : d.RoutingVersion != MarkerVersions.Grammar)) throw new FormatException("Invalid region data.");
            var source = new SourceSnapshot(d.Raw, d.Revision); if (source.Id != d.SourceId) throw new FormatException("Source identity mismatch.");
            return new AssistanceRegion(source, new TextSpan(d.Start, d.End), new TextSpan(d.ReplaceStart, d.ReplaceEnd),
                d.Open == null ? null : new MarkerConfiguration(d.Open, d.Close!), d.Profile == null ? null : new RegionIntent(d.Profile, d.Domain!), d.Closed);
        }
        private static string Pack<T>(string kind, T data)
        {
            var payload = Write(data); return Write(new Envelope { Version = AssistanceVersions.Contract, Kind = kind, Payload = payload, Sha256 = Hash(payload) });
        }
        private static T Unpack<T>(string json, string kind)
        {
            var envelope = Read<Envelope>(json);
            if (envelope.Version != AssistanceVersions.Contract || envelope.Kind != kind || envelope.Payload == null || envelope.Sha256 != Hash(envelope.Payload)) throw new FormatException("Assistance version/checksum mismatch.");
            return Read<T>(envelope.Payload);
        }
        private static string Hash(string value) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant(); }
        private static string Write<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                if (stream.Length > MaxCharacters) throw new FormatException("Assistance snapshot exceeds its budget.");
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        private static T Read<T>(string json)
        {
            if (json == null || json.Length > MaxCharacters) throw new FormatException("Assistance snapshot exceeds its budget.");
            var bytes = new UTF8Encoding(false, true).GetBytes(json);
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, new XmlDictionaryReaderQuotas { MaxDepth = 24, MaxStringContentLength = MaxCharacters, MaxArrayLength = MaxCharacters }))
                return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(reader)!;
        }
        [DataContract] private sealed class Envelope
        {
            [DataMember(Order=0,IsRequired=true)] public string? Version;
            [DataMember(Order=1,IsRequired=true)] public string? Kind;
            [DataMember(Order=2,IsRequired=true)] public string? Payload;
            [DataMember(Order=3,IsRequired=true)] public string? Sha256;
        }
        [DataContract] private sealed class RegionDto
        {
            [DataMember(Order=0,IsRequired=true)] public string? Raw;
            [DataMember(Order=1,IsRequired=true)] public long Revision;
            [DataMember(Order=2,IsRequired=true)] public string? SourceId;
            [DataMember(Order=3,IsRequired=true)] public int Start;
            [DataMember(Order=4,IsRequired=true)] public int End;
            [DataMember(Order=5,IsRequired=true)] public int ReplaceStart;
            [DataMember(Order=6,IsRequired=true)] public int ReplaceEnd;
            [DataMember(Order=7,IsRequired=true)] public string? Open;
            [DataMember(Order=8,IsRequired=true)] public string? Close;
            [DataMember(Order=9,IsRequired=true)] public string? Profile;
            [DataMember(Order=10,IsRequired=true)] public string? Domain;
            [DataMember(Order=11,IsRequired=true)] public string? RoutingVersion;
            [DataMember(Order=12,IsRequired=true)] public bool Closed;
        }
        [DataContract] private sealed class FactDto
        {
            [DataMember(Order=0,IsRequired=true)] public string? Key;
            [DataMember(Order=1,IsRequired=true)] public string? Value;
        }
        [DataContract] private sealed class ProposalDto
        {
            [DataMember(Order=0,IsRequired=true)] public string? Id;
            [DataMember(Order=1,IsRequired=true)] public string? Kind;
            [DataMember(Order=2,IsRequired=true)] public RegionDto? Region;
            [DataMember(Order=3,IsRequired=true)] public string? Settings;
            [DataMember(Order=4,IsRequired=true)] public FactDto[]? Conditions;
            [DataMember(Order=5,IsRequired=true)] public string? Result;
            [DataMember(Order=6,IsRequired=true)] public string? Original;
            [DataMember(Order=7,IsRequired=true)] public string? Solver;
            [DataMember(Order=8,IsRequired=true)] public string? Catalog;
            [DataMember(Order=9,IsRequired=true)] public string? Rule;
            [DataMember(Order=10,IsRequired=true)] public string[]? References;
        }
        [DataContract] private sealed class DraftDto
        {
            [DataMember(Order=0,IsRequired=true)] public string? Id;
            [DataMember(Order=1,IsRequired=true)] public RegionDto? Region;
            [DataMember(Order=2,IsRequired=true)] public string? Reactants;
            [DataMember(Order=3,IsRequired=true)] public int SeparatorStart;
            [DataMember(Order=4,IsRequired=true)] public int SeparatorEnd;
            [DataMember(Order=5,IsRequired=true)] public string? Arrow;
        }
    }
}
