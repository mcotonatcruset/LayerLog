namespace LayerLog.Core.Models;

public static class AttemptText
{
    // Wording shared by the history and the viewer: "intent" is one design attempt, "print" one physical run.
    public static string State(int prints) =>
        prints == 0 ? "Sense imprimir" : prints == 1 ? "1 impressió" : $"{prints} impressions";
}
