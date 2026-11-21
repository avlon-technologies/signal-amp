using System.IO;
using Microsoft.Data.Sqlite;

namespace WindowsMp3Player;

internal sealed record Track(long Id, string Path, string Title, string Artist = "", double? DurationSeconds = null)
{
    public string DurationLabel => DurationSeconds is double seconds
        ? TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss" : @"m\:ss")
        : "–";

    public override string ToString() => Title;
}

internal sealed class Catalog
{
    private readonly string database;

    internal Catalog(string? directory = null)
    {
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MusicPlayer");
        Directory.CreateDirectory(directory);
        database = Path.Combine(directory, "library.sqlite3");
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS tracks (id INTEGER PRIMARY KEY, path TEXT UNIQUE NOT NULL, title TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS playlists (name TEXT PRIMARY KEY);
            CREATE TABLE IF NOT EXISTS entries (
                playlist TEXT NOT NULL REFERENCES playlists(name) ON DELETE CASCADE,
                position INTEGER NOT NULL, track INTEGER NOT NULL REFERENCES tracks(id) ON DELETE CASCADE,
                PRIMARY KEY (playlist, position));
            CREATE TABLE IF NOT EXISTS audit (event TEXT NOT NULL, recorded_at TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP);
            """;
        command.ExecuteNonQuery();
        using var migration = connection.BeginTransaction();
        command.Transaction = migration;
        command.CommandText = "PRAGMA table_info(tracks)";
        var columns = new HashSet<string>();
        using (var schema = command.ExecuteReader())
            while (schema.Read()) columns.Add(schema.GetString(1));
        if (!columns.Contains("artist"))
        {
            command.CommandText = "ALTER TABLE tracks ADD COLUMN artist TEXT NOT NULL DEFAULT ''";
            command.ExecuteNonQuery();
        }
        if (!columns.Contains("duration"))
        {
            command.CommandText = "ALTER TABLE tracks ADD COLUMN duration REAL";
            command.ExecuteNonQuery();
        }
        migration.Commit();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = database, ForeignKeys = true }.ToString());
        connection.Open();
        return connection;
    }

    internal List<Track> Tracks(string? playlist = null)
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = playlist is null
            ? "SELECT id,path,title,artist,duration FROM tracks ORDER BY title COLLATE NOCASE,id"
            : "SELECT id,path,title,artist,duration FROM entries JOIN tracks ON entries.track=tracks.id WHERE playlist=$name ORDER BY position";
        if (playlist is not null) command.Parameters.AddWithValue("$name", playlist);
        using var reader = command.ExecuteReader();
        var tracks = new List<Track>();
        while (reader.Read()) tracks.Add(new Track(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetDouble(4)));
        return tracks;
    }

    internal List<string> Playlists()
    {
        using var connection = Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM playlists ORDER BY name COLLATE NOCASE";
        using var reader = command.ExecuteReader();
        var names = new List<string>();
        while (reader.Read()) names.Add(reader.GetString(0));
        return names;
    }

    internal void Import(string file)
    {
        var info = ReadMetadata(file);
        Write("Track imported", (connection, transaction) => Execute(connection, transaction, """
            INSERT INTO tracks(path,title,artist,duration) VALUES($path,$title,$artist,$duration)
            ON CONFLICT(path) DO UPDATE SET title=excluded.title,artist=excluded.artist,duration=excluded.duration
            """, ("$path", Path.GetFullPath(file)), ("$title", info.Title), ("$artist", info.Artist), ("$duration", info.Duration)));
    }

    internal void Relink(long id, string file)
    {
        var info = ReadMetadata(file);
        Write("Track relinked", (connection, transaction) => Execute(connection, transaction,
            "UPDATE tracks SET path=$path,title=$title,artist=$artist,duration=$duration WHERE id=$id",
            ("$path", Path.GetFullPath(file)), ("$title", info.Title), ("$artist", info.Artist),
            ("$duration", info.Duration), ("$id", id)));
    }

    private static (string Title, string Artist, double Duration) ReadMetadata(string file)
    {
        if (!Path.GetExtension(file).Equals(".mp3", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Choose an MP3 file.");
        try
        {
            using var metadata = TagLib.File.Create(file);
            var title = string.IsNullOrWhiteSpace(metadata.Tag.Title)
                ? Path.GetFileNameWithoutExtension(file)
                : metadata.Tag.Title.Trim();
            return (title, metadata.Tag.FirstPerformer ?? string.Empty, metadata.Properties.Duration.TotalSeconds);
        }
        catch (TagLib.CorruptFileException ex)
        {
            throw new IOException("The MP3 file is corrupt.", ex);
        }
        catch (TagLib.UnsupportedFormatException ex)
        {
            throw new IOException("The MP3 format is unsupported.", ex);
        }
    }

    internal void RemoveTrack(long id) => Write("Track removed", (connection, transaction) =>
        Execute(connection, transaction, "DELETE FROM tracks WHERE id=$id", ("$id", id)));

    internal void RenamePlaylist(string name, string replacement) => Write("Playlist renamed", (connection, transaction) =>
    {
        Execute(connection, transaction, "INSERT INTO playlists(name) VALUES($new)", ("$new", replacement));
        Execute(connection, transaction, "UPDATE entries SET playlist=$new WHERE playlist=$old", ("$new", replacement), ("$old", name));
        Execute(connection, transaction, "DELETE FROM playlists WHERE name=$old", ("$old", name));
    });

    internal void RemovePlaylist(string name) => Write("Playlist removed", (connection, transaction) =>
        Execute(connection, transaction, "DELETE FROM playlists WHERE name=$name", ("$name", name)));

    internal void Save(string name, IReadOnlyList<Track> tracks) => Write("Playlist saved", (connection, transaction) =>
    {
        Execute(connection, transaction, "INSERT OR IGNORE INTO playlists(name) VALUES($name)", ("$name", name));
        Execute(connection, transaction, "DELETE FROM entries WHERE playlist=$name", ("$name", name));
        for (var position = 0; position < tracks.Count; position++)
            Execute(connection, transaction, "INSERT INTO entries VALUES($name,$position,$id)",
                ("$name", name), ("$position", position), ("$id", tracks[position].Id));
    });

    private void Write(string action, Action<SqliteConnection, SqliteTransaction> operation)
    {
        using var connection = Open();
        using var transaction = connection.BeginTransaction();
        operation(connection, transaction);
        Execute(connection, transaction, "INSERT INTO audit(event) VALUES($event)", ("$event", action));
        transaction.Commit();
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql,
        params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }
}
