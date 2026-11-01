using LayerLog.Core.Data;

namespace LayerLog.Core.Models;

public static class AttemptText
{
    // Wording shared by the history and the viewer: "intent" is one design attempt, "print" one physical run.
    public static string State(int prints) =>
        prints == 0 ? "Sense imprimir" : prints == 1 ? "1 impressió" : $"{prints} impressions";

    /// <summary>Why the changes of an attempt may not be exact, or null when they are.</summary>
    public static string? Caution(GcodeEntry e)
    {
        var version = string.Join('.', e.Version.Split('.').Take(2));
        string?[] notes =
        [
            e.BaseApproximate && e.BaseSourceVersion is not null ? $"Comparació aproximada: no hi ha perfils de la versió {version}, s'ha fet servir la {e.BaseSourceVersion}." : null,
            e.BaseMissing ? "No s'ha trobat algun perfil base: mira la configuració completa." : null,
        ];
        return notes.Any(n => n is not null) ? string.Join('\n', notes.OfType<string>()) : null;
    }
}
