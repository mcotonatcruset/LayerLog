using System.Globalization;
using System.Text.Json;
using LayerLog.Core.Gcode;
using LayerLog.Core.Profiles;
using Microsoft.Data.Sqlite;

namespace LayerLog.Core.Data;

public sealed class HistoryRepository
{
    const string DateFormat = "yyyy-MM-dd";
    const string TimeFormat = "yyyy-MM-ddTHH:mm:ss";

    readonly string path;

    public HistoryRepository(string dbPath)
    {
        path = dbPath;
        using var c = Database.Open(path);
        Database.Migrate(c);
    }

    public long? FindByHash(string hash)
    {
        using var c = Database.Open(path);
        var value = Command(c, "SELECT id FROM gcode WHERE hash = $0", hash).ExecuteScalar();
        return value is null ? null : (long)value;
    }

    public long Add(string hash, string fileName, GcodeInfo g, BaseComparison comparison)
    {
        using var c = Database.Open(path);
        using var tx = c.BeginTransaction();
        var id = (long)Command(c, """
            INSERT INTO gcode (hash, file_name, generated_at, version, estimated_time, filament_grams, process_profile, filament_profile,
                config_json, process_changes_json, filament_changes_json, base_source_version, base_approximate, base_missing, thumbnail)
            VALUES ($0, $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14) RETURNING id
            """,
            hash, fileName, g.GeneratedAt.ToString(TimeFormat, CultureInfo.InvariantCulture), g.Version, g.EstimatedTime, g.FilamentGrams,
            g.ProcessProfile, g.FilamentProfile, JsonSerializer.Serialize(g.Config),
            JsonSerializer.Serialize(comparison.Process), JsonSerializer.Serialize(comparison.Filament),
            comparison.SourceVersion, comparison.Approximate, comparison.Missing, g.Thumbnail).ExecuteScalar()!;
        foreach (var name in g.Objects)
            Command(c, "INSERT INTO gcode_object (gcode_id, name) VALUES ($0, $1)", id, name).ExecuteNonQuery();
        tx.Commit();
        return id;
    }

    public void SetLocations(string volumeSerial, IReadOnlyDictionary<long, string> gcodePathsOnVolume)
    {
        using var c = Database.Open(path);
        using var tx = c.BeginTransaction();
        Command(c, "DELETE FROM gcode_location WHERE volume_serial = $0", volumeSerial).ExecuteNonQuery();
        foreach (var (id, relative) in gcodePathsOnVolume)
            Command(c, "INSERT INTO gcode_location (gcode_id, volume_serial, path) VALUES ($0, $1, $2)", id, volumeSerial, relative).ExecuteNonQuery();
        tx.Commit();
    }

    public IReadOnlyList<GcodeEntry> List(string? search = null)
    {
        var filter = string.IsNullOrWhiteSpace(search) ? "" :
            "WHERE g.file_name LIKE $0 OR EXISTS (SELECT 1 FROM gcode_object o WHERE o.gcode_id = g.id AND o.name LIKE $0)";
        return Entries(filter, string.IsNullOrWhiteSpace(search) ? [] : [$"%{search}%"]);
    }

    public IReadOnlyList<GcodeEntry> ListForObject(string objectName) =>
        Entries("WHERE EXISTS (SELECT 1 FROM gcode_object o WHERE o.gcode_id = g.id AND o.name = $0)", [objectName]);

    IReadOnlyList<GcodeEntry> Entries(string filter, object?[] args)
    {
        using var c = Database.Open(path);
        var list = new List<GcodeEntry>();
        using (var r = Command(c, $"SELECT g.* FROM gcode g {filter} ORDER BY g.generated_at DESC, g.id DESC", args).ExecuteReader())
            while (r.Read()) list.Add(ReadEntry(r));
        return list.Select(e => e with
        {
            Objects = Strings(c, "SELECT name FROM gcode_object WHERE gcode_id = $0 ORDER BY rowid", e.Id),
            VolumeSerials = Strings(c, "SELECT DISTINCT volume_serial FROM gcode_location WHERE gcode_id = $0", e.Id),
        }).ToList();
    }

    public long AddPrint(long gcodeId, DateOnly date, int? rating, string note, string? photoPath)
    {
        using var c = Database.Open(path);
        return (long)Command(c, "INSERT INTO print (gcode_id, date, rating, note, photo_path) VALUES ($0, $1, $2, $3, $4) RETURNING id",
            gcodeId, date.ToString(DateFormat, CultureInfo.InvariantCulture), rating, note, photoPath).ExecuteScalar()!;
    }

    public void UpdatePrint(PrintRecord p)
    {
        using var c = Database.Open(path);
        Command(c, "UPDATE print SET date = $0, rating = $1, note = $2, photo_path = $3 WHERE id = $4",
            p.Date.ToString(DateFormat, CultureInfo.InvariantCulture), p.Rating, p.Note, p.PhotoPath, p.Id).ExecuteNonQuery();
    }

    public void DeletePrint(long id)
    {
        using var c = Database.Open(path);
        Command(c, "DELETE FROM print WHERE id = $0", id).ExecuteNonQuery();
    }

    public IReadOnlyList<PrintRecord> Prints(long gcodeId)
    {
        using var c = Database.Open(path);
        return ReadPrints(Command(c, "SELECT * FROM print WHERE gcode_id = $0 ORDER BY date DESC, id DESC", gcodeId));
    }

    public PrintRecord? BestPrint(IEnumerable<long> gcodeIds)
    {
        using var c = Database.Open(path);
        var ids = string.Join(',', gcodeIds);
        return ReadPrints(Command(c, $"SELECT * FROM print WHERE gcode_id IN ({ids}) AND rating IS NOT NULL ORDER BY rating DESC, date DESC, id DESC LIMIT 1"))
            .FirstOrDefault();
    }

    static List<PrintRecord> ReadPrints(SqliteCommand command)
    {
        var list = new List<PrintRecord>();
        using var r = command.ExecuteReader();
        while (r.Read())
            list.Add(new PrintRecord(r.GetInt64(0), r.GetInt64(1), DateOnly.ParseExact(r.GetString(2), DateFormat, CultureInfo.InvariantCulture),
                r.IsDBNull(3) ? null : r.GetInt32(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5)));
        return list;
    }

    static GcodeEntry ReadEntry(SqliteDataReader r) => new(
        r.GetInt64(r.GetOrdinal("id")),
        r.GetString(r.GetOrdinal("file_name")),
        DateTime.ParseExact(r.GetString(r.GetOrdinal("generated_at")), TimeFormat, CultureInfo.InvariantCulture),
        r.GetString(r.GetOrdinal("version")),
        Nullable<string>(r, "estimated_time"),
        r.IsDBNull(r.GetOrdinal("filament_grams")) ? null : r.GetDouble(r.GetOrdinal("filament_grams")),
        r.GetString(r.GetOrdinal("process_profile")),
        r.GetString(r.GetOrdinal("filament_profile")),
        JsonSerializer.Deserialize<Dictionary<string, string>>(r.GetString(r.GetOrdinal("config_json")))!,
        JsonSerializer.Deserialize<List<ConfigChange>>(r.GetString(r.GetOrdinal("process_changes_json")))!,
        JsonSerializer.Deserialize<List<ConfigChange>>(r.GetString(r.GetOrdinal("filament_changes_json")))!,
        Nullable<string>(r, "base_source_version"),
        r.GetBoolean(r.GetOrdinal("base_approximate")),
        r.GetBoolean(r.GetOrdinal("base_missing")),
        Nullable<byte[]>(r, "thumbnail"),
        [], []);

    static T? Nullable<T>(SqliteDataReader r, string column) where T : class =>
        r.IsDBNull(r.GetOrdinal(column)) ? null : r.GetFieldValue<T>(r.GetOrdinal(column));

    static List<string> Strings(SqliteConnection c, string sql, params object?[] args)
    {
        var list = new List<string>();
        using var r = Command(c, sql, args).ExecuteReader();
        while (r.Read()) list.Add(r.GetString(0));
        return list;
    }

    static SqliteCommand Command(SqliteConnection c, string sql, params object?[] args)
    {
        var command = c.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < args.Length; i++)
            if (sql.Contains($"${i}")) command.Parameters.AddWithValue($"${i}", args[i] ?? DBNull.Value);
        return command;
    }
}
