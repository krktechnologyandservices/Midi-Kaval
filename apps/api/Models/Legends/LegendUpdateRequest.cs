using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Legends;

public record LegendUpdateRequest([MaxLength(256)] string Name);
