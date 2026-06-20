using System.ComponentModel.DataAnnotations;

namespace MidiKaval.Api.Models.Legends;

public record LegendCreateRequest([MaxLength(256)] string Name);
