namespace LayerLog.Core.Profiles;

public static class ConfigLabels
{
    static readonly Dictionary<string, (string Label, string Unit)> Known = new()
    {
        ["layer_height"] = ("Altura de capa", "mm"),
        ["wall_loops"] = ("Parets", ""),
        ["outer_wall_speed"] = ("Velocitat paret exterior", "mm/s"),
        ["inner_wall_speed"] = ("Velocitat paret interior", "mm/s"),
        ["default_acceleration"] = ("Acceleració per defecte", "mm/s²"),
        ["inner_wall_acceleration"] = ("Acceleració paret interior", "mm/s²"),
        ["raft_layers"] = ("Capes de balsa", ""),
        ["raft_contact_distance"] = ("Distància de contacte de la balsa", "mm"),
        ["enable_support"] = ("Suports", ""),
        ["support_type"] = ("Tipus de suport", ""),
        ["support_speed"] = ("Velocitat de suport", "mm/s"),
        ["support_interface_speed"] = ("Velocitat interfície de suport", "mm/s"),
        ["travel_speed"] = ("Velocitat de desplaçament", "mm/s"),
        ["initial_layer_infill_speed"] = ("Farciment primera capa", "mm/s"),
        ["nozzle_temperature"] = ("Temperatura del broquet", "°C"),
        ["nozzle_temperature_initial_layer"] = ("Broquet a la primera capa", "°C"),
        ["hot_plate_temp"] = ("Temperatura del llit", "°C"),
        ["hot_plate_temp_initial_layer"] = ("Llit a la primera capa", "°C"),
        ["textured_plate_temp"] = ("Llit texturat", "°C"),
        ["textured_plate_temp_initial_layer"] = ("Llit texturat, primera capa", "°C"),
        ["sparse_infill_density"] = ("Farciment", ""),
    };

    public static bool IsKnown(string key) => Known.ContainsKey(key);

    public static string Label(string key) => Known.TryGetValue(key, out var k) ? k.Label : key.Replace('_', ' ');

    public static string Format(string key, string value)
    {
        if (key == "enable_support") return value == "1" ? "Sí" : "No";
        var number = double.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out _) ? value.Replace('.', ',') : value;
        var unit = Known.TryGetValue(key, out var k) ? k.Unit : "";
        return unit.Length > 0 && !value.EndsWith('%') ? $"{number} {unit}" : number;
    }
}
