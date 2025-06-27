using Radzen;

namespace PrevencaoIncendio.Models;

public record Alerta(Variant Variant, AlertStyle Style, AlertSize Size, Shade Shade, string Icon, string Message, AlertLevel Level)
{
    public DateTime DataCriacao { get; set; } = DateTime.Now;
}
public enum AlertLevel
{
    Fire,
    Co,
    Warning,
    Danger,
    Info,
    MediaFire,
    MediaCo,
    MediaTemperatura,
    MediaUmidade,
    MediaPpm,
    MediaInfo,
}