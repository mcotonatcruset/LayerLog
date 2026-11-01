using System.Globalization;
using System.Text.Json;
using LayerLog.Core.Gcode;
using LayerLog.Core.Models;
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
        var inserted = Command(c, """
            INSERT INTO gcode (hash, file_name, generated_at, version, estimated_time, filament_grams, process_profile, filament_profile,
                config_json, process_changes_json, filament_changes_json, base_source_version, base_approximate, base_missing, thumbnail)
            VALUES ($0, $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14)
            ON CONFLICT (hash) DO NOTHING RETURNING id
            """,
            hash, fileName, g.GeneratedAt.ToString(TimeFormat, CultureInfo.InvariantCulture), g.Version, g.EstimatedTime, g.FilamentGrams,
            g.ProcessProfile, g.FilamentProfile, JsonSerializer.Serialize(g.Config),
            JsonSerializer.Serialize(comparison.Process), JsonSerializer.Serialize(comparison.Filament),
            comparison.SourceVersion, comparison.Approximate, comparison.Missing, g.Thumbnail).ExecuteScalar();
        if (inserted is null) return (long)Command(c, "SELECT id FROM gcode WHERE hash = $0", hash).ExecuteScalar()!;

        var id = (long)inserted;
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

    public GcodeEntry Get(long id) => Entries("WHERE g.id = $0", [id]).Single();

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

    public IReadOnlyList<GcodeEntry> ListForModel(string relativePath) =>
        Entries("WHERE EXISTS (SELECT 1 FROM gcode_object o WHERE o.gcode_id = g.id AND o.model_path = $0)", [relativePath]);

    /// <summary>One query for every figure: a gcode counts once per figure however many of its objects link to it.</summary>
    public IReadOnlyDictionary<string, FigureStats> FigureStats()
    {
        using var c = Database.Open(path);
        var rows = new List<(string Model, long Gcode, string At, string Profile, int? Rating, string? Date, long? PrintId)>();
        using (var r = Command(c, """
            SELECT DISTINCT o.model_path, g.id, g.generated_at, g.process_profile, p.rating, p.date, p.id
            FROM gcode_object o JOIN gcode g ON g.id = o.gcode_id LEFT JOIN print p ON p.gcode_id = g.id
            WHERE o.model_path IS NOT NULL
            """).ExecuteReader())
            while (r.Read())
                rows.Add((r.GetString(0), r.GetInt64(1), r.GetString(2), r.GetString(3),
                    r.IsDBNull(4) ? null : r.GetInt32(4), r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetInt64(6)));

        return rows.GroupBy(x => x.Model).ToDictionary(g => g.Key, g =>
        {
            var best = g.Where(x => x.Rating is not null)
                .OrderByDescending(x => x.Rating).ThenByDescending(x => x.Date, StringComparer.Ordinal).ThenByDescending(x => x.PrintId)
                .FirstOrDefault();
            return new FigureStats(g.Select(x => x.Gcode).Distinct().Count(), g.Select(x => x.PrintId).OfType<long>().Distinct().Count(),
                DateTime.ParseExact(g.Max(x => x.At)!, TimeFormat, CultureInfo.InvariantCulture), best.Rating, best.Profile);
        });
    }

    /// <summary>Gcodes none of whose linked models still exist, including gcodes never linked to any model.</summary>
    public IReadOnlyList<GcodeEntry> EntriesWithoutFigure(IReadOnlySet<string> existingModels)
    {
        var withFigure = new HashSet<long>();
        using (var c = Database.Open(path))
        using (var r = Command(c, "SELECT gcode_id, model_path FROM gcode_object WHERE model_path IS NOT NULL").ExecuteReader())
            while (r.Read())
                if (existingModels.Contains(r.GetString(1))) withFigure.Add(r.GetInt64(0));
        return Entries("", []).Where(e => !withFigure.Contains(e.Id)).ToList();
    }

    public IReadOnlyList<ObjectLink> ObjectLinks(long gcodeId)
    {
        using var c = Database.Open(path);
        var links = new List<ObjectLink>();
        using var r = Command(c, "SELECT name, model_path FROM gcode_object WHERE gcode_id = $0 ORDER BY rowid", gcodeId).ExecuteReader();
        while (r.Read()) links.Add(new ObjectLink(r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1)));
        return links;
    }

    public void LinkObject(long gcodeId, string objectName, string relativePath, string? fileHash, bool manual = true, long? size = null)
    {
        using var c = Database.Open(path);
        Command(c, "UPDATE gcode_object SET model_path = $0, model_hash = $1, model_size = $2, manual = $3 WHERE gcode_id = $4 AND name = $5",
            relativePath, fileHash, size, manual, gcodeId, objectName).ExecuteNonQuery();
    }

    public void LinkUnlinked(string root, IReadOnlyList<ModelFile> models)
    {
        foreach (var (gcodeId, name) in UnlinkedObjects())
        {
            var match = ModelLibrary.MatchObject(name, models);
            if (match is null) continue;
            var file = models.First(m => m.RelativePath == match);
            LinkObject(gcodeId, name, match, ModelLibrary.HashFile(Path.Combine(root, match)), manual: false, file.Size);
        }
    }

    // ponytail: hashes only same-size files, so the common case never reads the whole model folder.
    public void RelinkMovedModels(string root, IReadOnlyList<ModelFile> models)
    {
        using var c = Database.Open(path);
        var links = new List<(string Path, string Hash, long Size)>();
        using (var r = Command(c, "SELECT DISTINCT model_path, model_hash, model_size FROM gcode_object WHERE model_hash IS NOT NULL AND model_size IS NOT NULL").ExecuteReader())
            while (r.Read()) links.Add((r.GetString(0), r.GetString(1), r.GetInt64(2)));
        var metaLinks = new List<(string Path, string Hash, long Size)>();
        using (var r = Command(c, "SELECT relative_path, model_hash, model_size FROM figure_meta").ExecuteReader())
            while (r.Read()) metaLinks.Add((r.GetString(0), r.GetString(1), r.GetInt64(2)));

        foreach (var link in links.Concat(metaLinks).Where(l => !File.Exists(Path.Combine(root, l.Path))).DistinctBy(l => l.Path))
        {
            var moved = models.FirstOrDefault(m => m.Size == link.Size && ModelLibrary.HashFile(Path.Combine(root, m.RelativePath)) == link.Hash);
            if (moved is null) continue;
            Command(c, "UPDATE gcode_object SET model_path = $0 WHERE model_path = $1", moved.RelativePath, link.Path).ExecuteNonQuery();
            Command(c, "UPDATE OR IGNORE figure_meta SET relative_path = $0 WHERE relative_path = $1", moved.RelativePath, link.Path).ExecuteNonQuery();
        }
    }

    public IReadOnlyDictionary<string, FigureMeta> FigureMetas()
    {
        using var c = Database.Open(path);
        var metas = new Dictionary<string, FigureMeta>();
        using var r = Command(c, "SELECT relative_path, alias, is_part FROM figure_meta").ExecuteReader();
        while (r.Read()) metas[r.GetString(0)] = new FigureMeta(r.GetString(0), r.IsDBNull(1) ? null : r.GetString(1), r.GetBoolean(2));
        return metas;
    }

    public void SetAlias(string relativePath, string hash, long size, string? alias) =>
        Upsert("alias", relativePath, hash, size, string.IsNullOrWhiteSpace(alias) ? null : alias.Trim());

    public void SetIsPart(string relativePath, string hash, long size, bool isPart) =>
        Upsert("is_part", relativePath, hash, size, isPart);

    // column is always one of the two literals above, never user input
    void Upsert(string column, string relativePath, string hash, long size, object? value)
    {
        using var c = Database.Open(path);
        Command(c, $"""
            INSERT INTO figure_meta (relative_path, model_hash, model_size, {column}) VALUES ($0, $1, $2, $3)
            ON CONFLICT (relative_path) DO UPDATE SET model_hash = $1, model_size = $2, {column} = $3
            """, relativePath, hash, size, value).ExecuteNonQuery();
    }

    List<(long GcodeId, string Name)> UnlinkedObjects()
    {
        using var c = Database.Open(path);
        var list = new List<(long, string)>();
        using var r = Command(c, "SELECT gcode_id, name FROM gcode_object WHERE model_path IS NULL").ExecuteReader();
        while (r.Read()) list.Add((r.GetInt64(0), r.GetString(1)));
        return list;
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
