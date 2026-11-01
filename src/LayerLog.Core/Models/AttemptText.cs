using LayerLog.Core.Data;

namespace LayerLog.Core.Models;

public static class AttemptText
{
    // Wording shared by the panels: "intent" is one design attempt, "print" one physical run.
    public static string State(int prints) =>
        prints == 0 ? "Sense imprimir" : prints == 1 ? "1 impressió" : $"{prints} impressions";

    /// <summary>What deleting a print takes with it. The row goes, but nothing removes the photo's file.</summary>
    public static string DeleteWarning(PrintRecord p)
    {
        var lost = new List<string>();
        if (p.Rating is not null) lost.Add("la valoració");
        if (!string.IsNullOrWhiteSpace(p.Note)) lost.Add("la nota");
        var text = "Vols esborrar aquesta impressió?";
        if (lost.Count > 0) text += $" Es {(lost.Count == 1 ? "perdrà" : "perdran")} {string.Join(" i ", lost)}.";
        if (p.PhotoPath is not null) text += " El fitxer de la foto es queda al disc.";
        return text + " No es pot desfer.";
    }

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
