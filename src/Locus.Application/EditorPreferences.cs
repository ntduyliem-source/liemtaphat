using System.Text.Json;
using System.Text.Json.Serialization;
using Locus.Core.Detection;

namespace Locus.Application;

public sealed record EditorPreferences(int Version, FormulaSettings Settings, FormulaView View, bool AutoSave,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] bool? AcceptChemistrySpace=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] bool? AutoBalance=null)
{
    public static EditorPreferences Default => new(3, new(MarkerProfiles: MarkerPreferences.Default), new(), true);
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public string Serialize() { Validate(); return JsonSerializer.Serialize(this, Options); }
    public static EditorPreferences? Read(string? json)
    {
        if (json == null || json.Length > 8192) return null;
        try { var value = JsonSerializer.Deserialize<EditorPreferences>(json, Options); value?.Validate(); return value; }
        catch (Exception e) when (e is JsonException or FormatException or ArgumentException) { return null; }
    }
    private void Validate()
    {
        if (Version is not (1 or 2 or 3 or 4 or 5) || Version<5&&AutoBalance!=null || Version<4&&AcceptChemistrySpace!=null || Settings == null || View == null || !Settings.HasValidDomains || !Settings.HasValidMarkerProfiles ||
            (Version < 3 && Settings.MarkerProfiles != null) || (Version == 1 && Settings.EnabledDomains != DetectionDomains.Math) || !Enum.IsDefined(Settings.Mode) || Settings.Open == null || Settings.Close == null || Settings.Open.Length > 64 || Settings.Close.Length > 64)
            throw new FormatException("Unsupported preferences.");
        View.Validate();
    }
}
