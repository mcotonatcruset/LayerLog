using Microsoft.Data.Sqlite;

namespace LayerLog.Core.Data;

static class Database
{
    const string Schema = """
        CREATE TABLE gcode (
            id INTEGER PRIMARY KEY,
            hash TEXT NOT NULL UNIQUE,
            file_name TEXT NOT NULL,
            generated_at TEXT NOT NULL,
            version TEXT NOT NULL,
            estimated_time TEXT,
            filament_grams REAL,
            process_profile TEXT NOT NULL,
            filament_profile TEXT NOT NULL,
            config_json TEXT NOT NULL,
            process_changes_json TEXT NOT NULL,
            filament_changes_json TEXT NOT NULL,
            base_source_version TEXT,
            base_approximate INTEGER NOT NULL,
            base_missing INTEGER NOT NULL,
            thumbnail BLOB
        );
        CREATE TABLE gcode_object (
            gcode_id INTEGER NOT NULL REFERENCES gcode(id) ON DELETE CASCADE,
            name TEXT NOT NULL,
            PRIMARY KEY (gcode_id, name)
        );
        CREATE TABLE gcode_location (
            gcode_id INTEGER NOT NULL REFERENCES gcode(id) ON DELETE CASCADE,
            volume_serial TEXT NOT NULL,
            path TEXT NOT NULL,
            PRIMARY KEY (gcode_id, volume_serial, path)
        );
        CREATE TABLE print (
            id INTEGER PRIMARY KEY,
            gcode_id INTEGER NOT NULL REFERENCES gcode(id) ON DELETE CASCADE,
            date TEXT NOT NULL,
            rating INTEGER CHECK (rating BETWEEN 1 AND 5),
            note TEXT NOT NULL,
            photo_path TEXT
        );
        PRAGMA user_version = 1;
        """;

    const string ModelLinks = """
        ALTER TABLE gcode_object ADD COLUMN model_path TEXT;
        ALTER TABLE gcode_object ADD COLUMN model_hash TEXT;
        ALTER TABLE gcode_object ADD COLUMN model_size INTEGER;
        ALTER TABLE gcode_object ADD COLUMN manual INTEGER NOT NULL DEFAULT 0;
        PRAGMA user_version = 2;
        """;

    const string FigureMeta = """
        CREATE TABLE figure_meta (
            relative_path TEXT PRIMARY KEY,
            model_hash TEXT NOT NULL,
            model_size INTEGER NOT NULL,
            alias TEXT,
            is_part INTEGER NOT NULL DEFAULT 0
        );
        PRAGMA user_version = 3;
        """;

    public static SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString());
        connection.Open();
        Execute(connection, "PRAGMA foreign_keys = ON;");
        return connection;
    }

    public static void Migrate(SqliteConnection connection)
    {
        using var read = connection.CreateCommand();
        read.CommandText = "PRAGMA user_version;";
        var version = Convert.ToInt32(read.ExecuteScalar());
        if (version < 1) Execute(connection, Schema);
        if (version < 2) Execute(connection, ModelLinks);
        if (version < 3) Execute(connection, FigureMeta);
    }

    static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
