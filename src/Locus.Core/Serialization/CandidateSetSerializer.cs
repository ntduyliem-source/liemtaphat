using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Locus.Core.Detection;
using Locus.Core.Assistance;

namespace Locus.Core.Serialization
{
    /// <summary>Bounded, versioned local snapshots. SHA-256 detects corruption; it is not authentication.</summary>
    public static class CandidateSetSerializer
    {
        public const string EnvelopeVersion = "locus-json-envelope/1";
        public const int MaxSnapshotCharacters = 8 * 1024 * 1024;
        public const int MaxSourceCodeUnits = 1024 * 1024;
        public const int MaxNodeCount = 10000;
        public const int MaxNodeDepth = 128;
        private const int MaxReferenceCount = 50000;
        private const int MaxDiagnosticCount = 4096;
        private const int MaxEditCount = 4096;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public static string Serialize(CandidateSet candidateSet)
        {
            if (candidateSet == null) throw new ArgumentNullException(nameof(candidateSet));
            try
            {
                Check(candidateSet.Source.Raw.Length <= MaxSourceCodeUnits, "Source exceeds the snapshot size limit.");
                var limits = new Limits();
                var dto = new SnapshotDto
                {
                    SnapshotVersion = candidateSet.SchemaVersion,
                    CoreVersion = candidateSet.SchemaVersion == SmartChemistryVersions.Snapshot ? SmartChemistryVersions.Core : candidateSet.Intent != null ? MarkerVersions.Core : candidateSet.SchemaVersion == Versions.Snapshot ? Versions.Core : Locus.Core.Domains.DomainVersions.Core,
                    GrammarVersion = candidateSet.SchemaVersion == SmartChemistryVersions.Snapshot ? SmartChemistryVersions.Grammar : candidateSet.Intent != null ? MarkerVersions.Grammar : candidateSet.SchemaVersion == Versions.Snapshot ? Versions.Grammar : Locus.Core.Domains.DomainVersions.Grammar,
                    Source = new SourceDto
                    {
                        Id = candidateSet.Source.Id, Raw = candidateSet.Source.Raw,
                        Revision = candidateSet.Source.Revision, SchemaVersion = candidateSet.Source.SchemaVersion
                    },
                    ContentSpan = ToDto(candidateSet.ContentSpan), ReplacementSpan = ToDto(candidateSet.ReplacementSpan),
                    OriginalContent = candidateSet.OriginalContent, OriginalReplacement = candidateSet.OriginalReplacement,
                    Candidates = candidateSet.Candidates.Select(c => ToDto(c, limits)).ToArray(),
                    Diagnostics = ToDto(candidateSet.Diagnostics), SelectedCandidateId = candidateSet.SelectedCandidateId,
                    Markers = candidateSet.Markers == null ? null : new MarkersDto { Open = candidateSet.Markers.Open, Close = candidateSet.Markers.Close },
                    Intent = candidateSet.Intent == null ? null : new IntentDto { ProfileId = candidateSet.Intent.ProfileId, Domain = candidateSet.Intent.Domain, GrammarVersion = candidateSet.Intent.GrammarVersion }
                };
                // Use the same validation on both boundaries, including ID reconstruction.
                FromDto(dto);
                string payload = Write(dto);
                string json = Write(new EnvelopeDto { EnvelopeVersion = EnvelopeVersion, Payload = payload, Sha256 = Hash(payload) });
                Check(json.Length <= MaxSnapshotCharacters, "Serialized snapshot exceeds the size limit.");
                return json;
            }
            catch (Exception ex) when (IsDataError(ex))
            { throw new FormatException("Candidate set cannot be serialized as a valid Locus snapshot.", ex); }
        }

        public static CandidateSet Deserialize(string json)
        {
            if (json == null) throw new ArgumentNullException(nameof(json));
            try
            {
                Check(json.Length <= MaxSnapshotCharacters, "Snapshot exceeds the size limit.");
                var envelope = Read<EnvelopeDto>(json);
                Exact(envelope.EnvelopeVersion, EnvelopeVersion, "Unsupported snapshot envelope version.");
                Check(envelope.Payload != null && envelope.Payload.Length <= MaxSnapshotCharacters, "Missing or oversized snapshot payload.");
                Exact(envelope.Sha256, Hash(envelope.Payload!), "Snapshot checksum mismatch.");
                return FromDto(Read<SnapshotDto>(envelope.Payload!));
            }
            catch (Exception ex) when (IsDataError(ex))
            { throw new FormatException("Invalid or unsupported Locus candidate-set snapshot.", ex); }
        }

        private static bool IsDataError(Exception ex) => ex is ArgumentException || ex is SerializationException ||
            ex is XmlException || ex is InvalidOperationException || ex is FormatException || ex is OverflowException;

        private static CandidateSet FromDto(SnapshotDto dto)
        {
            bool legacy = dto.SnapshotVersion == Versions.Snapshot;
            bool routed = dto.SnapshotVersion == MarkerVersions.Snapshot;
            bool smart = dto.SnapshotVersion == SmartChemistryVersions.Snapshot;
            Check(smart || routed || legacy || dto.SnapshotVersion == Locus.Core.Domains.DomainVersions.Snapshot, "Unsupported candidate-set schema version.");
            Exact(dto.CoreVersion, smart ? SmartChemistryVersions.Core : routed ? MarkerVersions.Core : legacy ? Versions.Core : Locus.Core.Domains.DomainVersions.Core, "Unsupported historical core version; migration is not implicit.");
            Exact(dto.GrammarVersion, smart ? SmartChemistryVersions.Grammar : routed ? MarkerVersions.Grammar : legacy ? Versions.Grammar : Locus.Core.Domains.DomainVersions.Grammar, "Unsupported historical grammar version; reparsing is not restoration.");
            Check(smart || routed == (dto.Intent != null), "Region intent/schema mismatch.");
            Check(dto.Source != null, "Missing source snapshot.");
            Exact(dto.Source!.SchemaVersion, Versions.Contract, "Unsupported source contract version.");
            Check(dto.Source.Raw != null && dto.Source.Raw.Length <= MaxSourceCodeUnits, "Missing or oversized source.");
            var source = new SourceSnapshot(dto.Source.Raw!, dto.Source.Revision);
            Exact(source.Id, dto.Source.Id, "Source identity does not match its raw text/revision.");
            var content = FromDto(dto.ContentSpan);
            var replacement = FromDto(dto.ReplacementSpan);
            Exact(dto.OriginalContent, source.Slice(content), "Original content does not match source coordinates.");
            Exact(dto.OriginalReplacement, source.Slice(replacement), "Original replacement does not match source coordinates.");
            Check(dto.Candidates != null && dto.Candidates.Length <= 3, "Missing or excessive candidate list.");
            var limits = new Limits();
            var candidates = dto.Candidates!.Select(c => FromDto(c, source, limits)).ToArray();
            Check(smart == candidates.Any(c => c.GrammarVersion == SmartChemistryVersions.Grammar), "Chemistry grammar/snapshot mismatch.");
            Check(smart || routed || legacy == candidates.All(c => c.Document.Domain == "math"), "Domain set/snapshot version mismatch.");
            MarkerConfiguration? markers = null;
            if (dto.Markers != null)
            {
                Check(dto.Markers.Open != null && dto.Markers.Close != null, "Missing marker literals.");
                markers = new MarkerConfiguration(dto.Markers.Open!, dto.Markers.Close!);
            }
            RegionIntent? intent = null;
            if (dto.Intent != null)
            {
                Exact(dto.Intent.GrammarVersion, MarkerVersions.Grammar, "Unsupported region routing version.");
                intent = new RegionIntent(dto.Intent.ProfileId, dto.Intent.Domain);
            }
            return new CandidateSet(source, content, replacement, candidates, FromDto(dto.Diagnostics), dto.SelectedCandidateId, markers, intent);
        }

        private static CandidateDto ToDto(Candidate candidate, Limits limits)
        {
            Check(candidate.Edits.Count <= MaxEditCount, "Too many source edits.");
            return new CandidateDto
            {
                Id = candidate.Id, Kind = candidate.Kind, SourceId = candidate.Source.Id,
                CoreVersion = candidate.CoreVersion, GrammarVersion = candidate.GrammarVersion,
                DocumentVersion = candidate.Document.SchemaVersion, Domain = candidate.Document.Domain,
                Root = ToDto(candidate.Document.Root, limits, 1), ContentSpan = ToDto(candidate.ContentSpan),
                ReplacementSpan = ToDto(candidate.ReplacementSpan), Diagnostics = ToDto(candidate.Diagnostics),
                Edits = candidate.Edits.Select(e => new EditDto { Span = ToDto(e.Span), Text = e.Text }).ToArray(),
                Provenance = candidate.Provenance
            };
        }

        private static Candidate FromDto(CandidateDto dto, SourceSnapshot source, Limits limits)
        {
            Check(dto != null, "Null candidate.");
            Exact(dto!.SourceId, source.Id, "Candidate references a different source snapshot.");
            var document = Locus.Core.Domains.ScientificDocument.Create(dto.Domain ?? "", FromDto(dto.Root, limits, 1), dto.GrammarVersion);
            Exact(dto.CoreVersion, document.CoreVersion, "Unsupported candidate core version.");
            Exact(dto.GrammarVersion, document.GrammarVersion, "Unsupported candidate grammar version.");
            Exact(dto.DocumentVersion, document.SchemaVersion, "Unsupported document schema.");
            Check(dto.Edits != null && dto.Edits.Length <= MaxEditCount, "Missing or excessive source-edit list.");
            var edits = dto.Edits!.Select(e =>
            {
                Check(e != null && e.Text != null, "Invalid source edit.");
                return new SourceEdit(FromDto(e!.Span), e.Text!);
            }).ToArray();
            Check(dto.Kind != null && dto.Provenance != null, "Missing candidate kind/provenance.");
            var candidate = new Candidate(dto.Kind!, document, source,
                FromDto(dto.ContentSpan), FromDto(dto.ReplacementSpan), FromDto(dto.Diagnostics), edits, dto.Provenance);
            Exact(candidate.Id, dto.Id, "Candidate identity does not match its stored structure/source.");
            return candidate;
        }

        private static NodeDto ToDto(MathNode node, Limits limits, int depth)
        {
            limits.Node(depth, node.SourceSpans.Count);
            return new NodeDto
            {
                Id = node.Id, Type = node.Type, Value = node.Value, Name = node.Name, Operator = node.Operator,
                Children = node.Children.Select(c => ToDto(c, limits, depth + 1)).ToArray(),
                SourceSpans = node.SourceSpans.Select(ToDto).ToArray()
            };
        }

        private static MathNode FromDto(NodeDto dto, Limits limits, int depth)
        {
            Check(dto != null && dto.Children != null && dto.SourceSpans != null, "Missing AST node, children or source references.");
            limits.Node(depth, dto!.SourceSpans!.Length);
            Check(dto.Children!.Length <= 2, "Excessive AST arity.");
            Check(dto.Type != null, "Missing node type.");
            var node = new MathNode(dto.Type!, dto.Children.Select(c => FromDto(c, limits, depth + 1)),
                dto.Value, dto.Name, dto.Operator, dto.SourceSpans.Select(FromDto));
            Exact(node.Id, dto.Id, "AST node identity does not match its structure/source references.");
            return node;
        }

        private static DiagnosticDto[] ToDto(IReadOnlyList<Diagnostic> diagnostics)
        {
            Check(diagnostics.Count <= MaxDiagnosticCount, "Too many diagnostics.");
            return diagnostics.Select(d => new DiagnosticDto { Code = d.Code, Severity = d.Severity, Span = ToDto(d.Span), Message = d.Message }).ToArray();
        }

        private static Diagnostic[] FromDto(DiagnosticDto[] diagnostics)
        {
            Check(diagnostics != null && diagnostics.Length <= MaxDiagnosticCount, "Missing or excessive diagnostics.");
            return diagnostics!.Select(d =>
            {
                Check(d != null && d.Code != null && d.Severity != null && d.Message != null, "Invalid diagnostic.");
                return new Diagnostic(d!.Code!, d.Severity!, FromDto(d.Span), d.Message);
            }).ToArray();
        }

        private static SpanDto ToDto(TextSpan span) => new SpanDto { Start = span.Start, End = span.End };
        private static TextSpan FromDto(SpanDto span)
        {
            Check(span != null, "Missing source span.");
            return new TextSpan(span!.Start, span.End);
        }

        private static void Exact(string? actual, string? expected, string message) => Check(actual != null && expected != null && string.Equals(actual, expected, StringComparison.Ordinal), message);
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new FormatException(message);
        }

        private static string Hash(string payload)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(payload))).Replace("-", "").ToLowerInvariant();
        }

        private static string Write<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 250000 }).WriteObject(stream, value);
                return Utf8.GetString(stream.ToArray());
            }
        }

        private static T Read<T>(string json)
        {
            ValidateSingleJsonObject(json);
            var bytes = Utf8.GetBytes(json);
            var quotas = new XmlDictionaryReaderQuotas
            {
                // DTO object + child array contribute more than one JSON level per AST level.
                MaxDepth = MaxNodeDepth * 3 + 32,
                MaxStringContentLength = MaxSnapshotCharacters,
                MaxArrayLength = MaxReferenceCount,
                MaxBytesPerRead = 16384,
                MaxNameTableCharCount = 16384
            };
            using (var reader = JsonReaderWriterFactory.CreateJsonReader(bytes, quotas))
            {
                var value = new DataContractJsonSerializer(typeof(T), new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 250000 }).ReadObject(reader);
                Check(value is T, "Unexpected snapshot value.");
                while (reader.Read())
                    Check(reader.NodeType == XmlNodeType.Whitespace || reader.NodeType == XmlNodeType.SignificantWhitespace, "Trailing data after snapshot.");
                return (T)value!;
            }
        }

        // The BCL JSON reader can stop after the first value and silently ignore
        // trailing bytes. Check the complete outer boundary before handing it data.
        private static void ValidateSingleJsonObject(string json)
        {
            int index = 0;
            while (index < json.Length && IsJsonSpace(json[index])) index++;
            Check(index < json.Length && json[index] == '{', "Snapshot must be one JSON object.");
            var brackets = new char[MaxNodeDepth * 3 + 32];
            int depth = 0;
            bool quoted = false, escaped = false;
            for (; index < json.Length; index++)
            {
                char ch = json[index];
                if (quoted)
                {
                    Check(ch >= ' ', "Unescaped control character in JSON string.");
                    if (escaped) escaped = false;
                    else if (ch == '\\') escaped = true;
                    else if (ch == '"') quoted = false;
                    continue;
                }
                if (ch == '"') { quoted = true; continue; }
                if (ch == '{' || ch == '[')
                {
                    Check(depth < brackets.Length, "JSON nesting exceeds the snapshot limit.");
                    brackets[depth++] = ch;
                }
                else if (ch == '}' || ch == ']')
                {
                    Check(depth > 0 && brackets[depth - 1] == (ch == '}' ? '{' : '['), "Mismatched JSON boundary.");
                    depth--;
                    if (depth == 0)
                    {
                        for (index++; index < json.Length; index++) Check(IsJsonSpace(json[index]), "Trailing data after snapshot.");
                        return;
                    }
                }
            }
            throw new FormatException("Incomplete JSON snapshot.");
        }

        private static bool IsJsonSpace(char ch) => ch == ' ' || ch == '\t' || ch == '\r' || ch == '\n';

        private sealed class Limits
        {
            private int nodes;
            private int references;
            public void Node(int depth, int sourceReferences)
            {
                Check(depth <= MaxNodeDepth && ++nodes <= MaxNodeCount, "Snapshot AST depth/count exceeds limits.");
                references += sourceReferences;
                Check(sourceReferences > 0 && references <= MaxReferenceCount, "Missing or excessive AST source references.");
            }
        }

        [DataContract]
        private sealed class EnvelopeDto
        {
            [DataMember(Name = "envelopeVersion", Order = 0, IsRequired = true)] public string EnvelopeVersion = null!;
            [DataMember(Name = "sha256", Order = 1, IsRequired = true)] public string Sha256 = null!;
            [DataMember(Name = "payload", Order = 2, IsRequired = true)] public string Payload = null!;
        }

        [DataContract]
        private sealed class SnapshotDto
        {
            [DataMember(Name = "snapshotVersion", Order = 0, IsRequired = true)] public string SnapshotVersion = null!;
            [DataMember(Name = "coreVersion", Order = 1, IsRequired = true)] public string CoreVersion = null!;
            [DataMember(Name = "grammarVersion", Order = 2, IsRequired = true)] public string GrammarVersion = null!;
            [DataMember(Name = "source", Order = 3, IsRequired = true)] public SourceDto Source = null!;
            [DataMember(Name = "contentSpan", Order = 4, IsRequired = true)] public SpanDto ContentSpan = null!;
            [DataMember(Name = "replacementSpan", Order = 5, IsRequired = true)] public SpanDto ReplacementSpan = null!;
            [DataMember(Name = "originalContent", Order = 6, IsRequired = true)] public string OriginalContent = null!;
            [DataMember(Name = "originalReplacement", Order = 7, IsRequired = true)] public string OriginalReplacement = null!;
            [DataMember(Name = "candidates", Order = 8, IsRequired = true)] public CandidateDto[] Candidates = null!;
            [DataMember(Name = "diagnostics", Order = 9, IsRequired = true)] public DiagnosticDto[] Diagnostics = null!;
            [DataMember(Name = "selectedCandidateId", Order = 10, IsRequired = true)] public string? SelectedCandidateId;
            [DataMember(Name = "markers", Order = 11, IsRequired = true)] public MarkersDto? Markers;
            [DataMember(Name = "intent", Order = 12, EmitDefaultValue = false)] public IntentDto? Intent;
        }

        [DataContract]
        private sealed class SourceDto
        {
            [DataMember(Name = "id", Order = 0, IsRequired = true)] public string Id = null!;
            [DataMember(Name = "schemaVersion", Order = 1, IsRequired = true)] public string SchemaVersion = null!;
            [DataMember(Name = "raw", Order = 2, IsRequired = true)] public string Raw = null!;
            [DataMember(Name = "revision", Order = 3, IsRequired = true)] public long Revision;
        }

        [DataContract]
        private sealed class IntentDto
        {
            [DataMember(Name = "profileId", Order = 0, IsRequired = true)] public string ProfileId = null!;
            [DataMember(Name = "domain", Order = 1, IsRequired = true)] public string Domain = null!;
            [DataMember(Name = "grammarVersion", Order = 2, IsRequired = true)] public string GrammarVersion = null!;
        }

        [DataContract]
        private sealed class MarkersDto
        {
            [DataMember(Name = "open", Order = 0, IsRequired = true)] public string Open = null!;
            [DataMember(Name = "close", Order = 1, IsRequired = true)] public string Close = null!;
        }

        [DataContract]
        private sealed class CandidateDto
        {
            [DataMember(Name = "id", Order = 0, IsRequired = true)] public string Id = null!;
            [DataMember(Name = "kind", Order = 1, IsRequired = true)] public string Kind = null!;
            [DataMember(Name = "sourceId", Order = 2, IsRequired = true)] public string SourceId = null!;
            [DataMember(Name = "coreVersion", Order = 3, IsRequired = true)] public string CoreVersion = null!;
            [DataMember(Name = "grammarVersion", Order = 4, IsRequired = true)] public string GrammarVersion = null!;
            [DataMember(Name = "documentVersion", Order = 5, IsRequired = true)] public string DocumentVersion = null!;
            [DataMember(Name = "domain", Order = 6, IsRequired = true)] public string Domain = null!;
            [DataMember(Name = "root", Order = 7, IsRequired = true)] public NodeDto Root = null!;
            [DataMember(Name = "contentSpan", Order = 8, IsRequired = true)] public SpanDto ContentSpan = null!;
            [DataMember(Name = "replacementSpan", Order = 9, IsRequired = true)] public SpanDto ReplacementSpan = null!;
            [DataMember(Name = "diagnostics", Order = 10, IsRequired = true)] public DiagnosticDto[] Diagnostics = null!;
            [DataMember(Name = "edits", Order = 11, IsRequired = true)] public EditDto[] Edits = null!;
            [DataMember(Name = "provenance", Order = 12, IsRequired = true)] public string Provenance = null!;
        }

        [DataContract]
        private sealed class NodeDto
        {
            [DataMember(Name = "id", Order = 0, IsRequired = true)] public string Id = null!;
            [DataMember(Name = "type", Order = 1, IsRequired = true)] public string Type = null!;
            [DataMember(Name = "value", Order = 2, IsRequired = true)] public string? Value;
            [DataMember(Name = "name", Order = 3, IsRequired = true)] public string? Name;
            [DataMember(Name = "operator", Order = 4, IsRequired = true)] public string? Operator;
            [DataMember(Name = "children", Order = 5, IsRequired = true)] public NodeDto[] Children = null!;
            [DataMember(Name = "sourceSpans", Order = 6, IsRequired = true)] public SpanDto[] SourceSpans = null!;
        }

        [DataContract]
        private sealed class SpanDto
        {
            [DataMember(Name = "start", Order = 0, IsRequired = true)] public int Start;
            [DataMember(Name = "end", Order = 1, IsRequired = true)] public int End;
        }

        [DataContract]
        private sealed class DiagnosticDto
        {
            [DataMember(Name = "code", Order = 0, IsRequired = true)] public string Code = null!;
            [DataMember(Name = "severity", Order = 1, IsRequired = true)] public string Severity = null!;
            [DataMember(Name = "span", Order = 2, IsRequired = true)] public SpanDto Span = null!;
            [DataMember(Name = "message", Order = 3, IsRequired = true)] public string Message = null!;
        }

        [DataContract]
        private sealed class EditDto
        {
            [DataMember(Name = "span", Order = 0, IsRequired = true)] public SpanDto Span = null!;
            [DataMember(Name = "text", Order = 1, IsRequired = true)] public string Text = null!;
        }
    }
}
