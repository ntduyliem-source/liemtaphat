using System;
using System.Linq;
using System.Text;
using Locus.Core;
using Locus.Core.Serialization;

namespace Locus.Word;

public sealed class ManagedSnapshot
{
    public const string Prefix = "locus:word:1:";
    public const string SmartPrefix = "locus:word:2:";
    public const int MaxTagCodeUnits = 65536;
    public string EntryId { get; }
    public CandidateSet Candidates => State.Readings ?? State.Result!;
    public ManualFormulaState State { get; }
    public string OriginalSource => State.OriginalSource;
    public Candidate Selected => State.Selected!;
    private readonly bool legacy;
    public ManagedSnapshot(CandidateSet candidates, string? entryId = null)
        : this(new ManualFormulaState(candidates), entryId, true) { }
    public ManagedSnapshot(ManualFormulaState state, string? entryId = null) : this(state, entryId, false) { }
    private ManagedSnapshot(ManualFormulaState state, string? entryId, bool legacy)
    {
        State = state ?? throw new ArgumentNullException(nameof(state)); this.legacy = legacy;
        if (state.Selected == null) throw new ArgumentException("Choose a complete result explicitly.");
        EntryId = entryId ?? Guid.NewGuid().ToString("N");
        if (!Guid.TryParseExact(EntryId, "N", out _)) throw new ArgumentException("Invalid entry ID.");
    }
    public string Encode()
    {
        string payload = legacy ? CandidateSetSerializer.Serialize(Candidates) : ManualStateCodec.Serialize(State);
        string value = (legacy ? Prefix : SmartPrefix) + EntryId + ":" + (legacy ? "" : ManualStateCodec.Digest(payload) + ":") + Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        if (value.Length > MaxTagCodeUnits) throw new InvalidOperationException("Snapshot exceeds the tested Word metadata limit; source is unchanged.");
        return value;
    }
    public static ManagedSnapshot Decode(string value)
    {
        bool legacy = value != null && value.StartsWith(Prefix, StringComparison.Ordinal);
        if (value == null || value.Length > MaxTagCodeUnits || !legacy && !value.StartsWith(SmartPrefix, StringComparison.Ordinal)) throw new FormatException("Unknown or oversized Word metadata.");
        string[] parts = value.Substring(legacy ? Prefix.Length : SmartPrefix.Length).Split(':');
        if (parts.Length != (legacy ? 2 : 3)) throw new FormatException("Malformed Word metadata.");
        try {
            string payload = new UTF8Encoding(false,true).GetString(Convert.FromBase64String(parts[legacy ? 1 : 2]));
            if (!legacy && ManualStateCodec.Digest(payload) != parts[1]) throw new FormatException("Word snapshot checksum differs.");
            return legacy ? new ManagedSnapshot(CandidateSetSerializer.Deserialize(payload), parts[0]) : new ManagedSnapshot(ManualStateCodec.Deserialize(payload), parts[0]);
        }
        catch (Exception e) when (e is ArgumentException || e is InvalidOperationException || e is System.Runtime.Serialization.SerializationException) { throw new FormatException("Invalid Word metadata.", e); }
    }
    public static bool HasEntry(string tag, string entryId) => tag.StartsWith(Prefix + entryId + ":", StringComparison.Ordinal) || tag.StartsWith(SmartPrefix + entryId + ":", StringComparison.Ordinal);
}
