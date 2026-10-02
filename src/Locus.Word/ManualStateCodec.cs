using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using Locus.Core.Assistance;
using Locus.Core.Serialization;

namespace Locus.Word;

internal static class ManualStateCodec
{
    [DataContract] private sealed class Data
    {
        [DataMember(Order=0)] public int Version = 1;
        [DataMember(Order=1, EmitDefaultValue=false)] public string? Readings;
        [DataMember(Order=2, EmitDefaultValue=false)] public string? Draft;
        [DataMember(Order=3, EmitDefaultValue=false)] public string? Products;
        [DataMember(Order=4, EmitDefaultValue=false)] public string? Result;
        [DataMember(Order=5, EmitDefaultValue=false)] public string? BeforeBalance;
        [DataMember(Order=6)] public bool KeepFromAuto;
        [DataMember(Order=7, EmitDefaultValue=false)] public string? BalanceSolver;
    }
    public static string Serialize(ManualFormulaState state)
    {
        var data = new Data { Readings=state.Readings==null?null:CandidateSetSerializer.Serialize(state.Readings),
            Draft=state.Draft==null?null:AssistanceSerializer.SerializeDraft(state.Draft), Products=state.Products==null?null:AssistanceSerializer.Serialize(state.Products),
            Result=state.Result==null?null:CandidateSetSerializer.Serialize(state.Result), BeforeBalance=state.BeforeBalance==null?null:CandidateSetSerializer.Serialize(state.BeforeBalance),
            KeepFromAuto=state.KeepFromAuto, BalanceSolver=state.CanCancelBalance?AssistanceVersions.Solver:null };
        using var stream = new MemoryStream(); new DataContractJsonSerializer(typeof(Data)).WriteObject(stream, data);
        return Encoding.UTF8.GetString(stream.ToArray());
    }
    public static ManualFormulaState Deserialize(string value)
    {
        if (value.Length > ManagedSnapshot.MaxTagCodeUnits) throw new FormatException("Oversized Word state.");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(value));
        var data = (Data?)new DataContractJsonSerializer(typeof(Data)).ReadObject(stream) ?? throw new FormatException("Missing Word state.");
        if (data.Version != 1 || (data.BeforeBalance != null ? data.BalanceSolver != AssistanceVersions.Solver : data.BalanceSolver != null))
            throw new FormatException("Unsupported Word state version.");
        return new ManualFormulaState(data.Readings==null?null:CandidateSetSerializer.Deserialize(data.Readings), data.Draft==null?null:AssistanceSerializer.DeserializeDraft(data.Draft),
            data.Products==null?null:AssistanceSerializer.Deserialize(data.Products), data.Result==null?null:CandidateSetSerializer.Deserialize(data.Result),
            data.BeforeBalance==null?null:CandidateSetSerializer.Deserialize(data.BeforeBalance), data.KeepFromAuto);
    }
    public static string Digest(string value)
    {
        using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
    }
}
