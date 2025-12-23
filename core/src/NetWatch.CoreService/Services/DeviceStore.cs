using Microsoft.Data.Sqlite;
using NetWatch.CoreService.Models;
using NetWatch.CoreService.Services.Classification;

namespace NetWatch.CoreService.Services;

public sealed class DeviceStore
{
    private readonly string _connectionString;
    private readonly ILogger<DeviceStore> _logger;
    private readonly EventHub _eventHub;
    private readonly DeviceClassifier _classifier;

    public DeviceStore(string dbPath, ILogger<DeviceStore> logger, EventHub eventHub, DeviceClassifier classifier)
    {
        _connectionString = $"Data Source={dbPath}";
        _logger = logger;
        _eventHub = eventHub;
        _classifier = classifier;
        EnsureSchema();
    }

    public IReadOnlyCollection<Device> GetAll()
    {
        var devices = new List<Device>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT device_id, mac_address, ip_address, hostname, vendor, type_guess,
                   first_seen_utc, last_seen_utc, confidence, is_online
            FROM devices
            ORDER BY last_seen_utc DESC
            """;

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            devices.Add(MapDevice(reader));
        }

        return devices.AsReadOnly();
    }

    public IReadOnlyCollection<DeviceEvent> GetEvents(int limit = 200)
    {
        var events = new List<DeviceEvent>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT event_id, device_id, event_type, occurred_at_utc, detail
            FROM device_events
            ORDER BY occurred_at_utc DESC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            events.Add(new DeviceEvent
            {
                EventId = reader.GetInt64(0),
                DeviceId = reader.GetString(1),
                EventType = reader.GetString(2),
                OccurredAtUtc = DateTime.Parse(reader.GetString(3)),
                Detail = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return events.AsReadOnly();
    }

    public IReadOnlyCollection<DeviceEvent> GetEventsSince(DateTime sinceUtc, int limit = 2000)
    {
        var events = new List<DeviceEvent>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT event_id, device_id, event_type, occurred_at_utc, detail
            FROM device_events
            WHERE occurred_at_utc >= $since
            ORDER BY occurred_at_utc ASC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$since", sinceUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            events.Add(new DeviceEvent
            {
                EventId = reader.GetInt64(0),
                DeviceId = reader.GetString(1),
                EventType = reader.GetString(2),
                OccurredAtUtc = DateTime.Parse(reader.GetString(3)),
                Detail = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return events.AsReadOnly();
    }

    public IReadOnlyCollection<DeviceEvent> GetEventsForDeviceSince(string deviceId, DateTime sinceUtc, int limit = 2000)
    {
        var events = new List<DeviceEvent>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT event_id, device_id, event_type, occurred_at_utc, detail
            FROM device_events
            WHERE device_id = $deviceId AND occurred_at_utc >= $since
            ORDER BY occurred_at_utc ASC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$deviceId", deviceId);
        cmd.Parameters.AddWithValue("$since", sinceUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            events.Add(new DeviceEvent
            {
                EventId = reader.GetInt64(0),
                DeviceId = reader.GetString(1),
                EventType = reader.GetString(2),
                OccurredAtUtc = DateTime.Parse(reader.GetString(3)),
                Detail = reader.IsDBNull(4) ? null : reader.GetString(4)
            });
        }

        return events.AsReadOnly();
    }

    public IReadOnlyCollection<DeviceObservation> GetObservationsForDevice(string deviceId, int limit = 100)
    {
        var observations = new List<DeviceObservation>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT observation_id, source, mac_address, ip_address, hostname, vendor, type_hint, service_hint, observed_at_utc
            FROM observations
            WHERE device_id = $deviceId
            ORDER BY observed_at_utc DESC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$deviceId", deviceId);
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            observations.Add(new DeviceObservation
            {
                ObservationId = reader.GetInt64(0),
                Source = reader.GetString(1),
                MacAddress = reader.IsDBNull(2) ? null : reader.GetString(2),
                IpAddress = reader.IsDBNull(3) ? null : reader.GetString(3),
                Hostname = reader.IsDBNull(4) ? null : reader.GetString(4),
                Vendor = reader.IsDBNull(5) ? null : reader.GetString(5),
                TypeHint = reader.IsDBNull(6) ? null : reader.GetString(6),
                ServiceHint = reader.IsDBNull(7) ? null : reader.GetString(7),
                ObservedAtUtc = DateTime.Parse(reader.GetString(8))
            });
        }

        return observations.AsReadOnly();
    }

    public IReadOnlyCollection<DeviceObservation> GetRecentObservations(int limit = 200)
    {
        var observations = new List<DeviceObservation>();
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT observation_id, source, mac_address, ip_address, hostname, vendor, type_hint, service_hint, observed_at_utc
            FROM observations
            ORDER BY observed_at_utc DESC
            LIMIT $limit
            """;
        cmd.Parameters.AddWithValue("$limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            observations.Add(new DeviceObservation
            {
                ObservationId = reader.GetInt64(0),
                Source = reader.GetString(1),
                MacAddress = reader.IsDBNull(2) ? null : reader.GetString(2),
                IpAddress = reader.IsDBNull(3) ? null : reader.GetString(3),
                Hostname = reader.IsDBNull(4) ? null : reader.GetString(4),
                Vendor = reader.IsDBNull(5) ? null : reader.GetString(5),
                TypeHint = reader.IsDBNull(6) ? null : reader.GetString(6),
                ServiceHint = reader.IsDBNull(7) ? null : reader.GetString(7),
                ObservedAtUtc = DateTime.Parse(reader.GetString(8))
            });
        }

        return observations.AsReadOnly();
    }

    public Device UpsertFromObservation(Observation observation)
    {
        var observedAt = observation.ObservedAtUtc == default ? DateTime.UtcNow : observation.ObservedAtUtc;
        var mac = NormalizeMac(observation.MacAddress);
        var ip = observation.IpAddress;

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var tx = connection.BeginTransaction();

        var device = FindDevice(connection, mac, ip);
        var isNew = device is null;

        if (isNew)
        {
            device = new Device
            {
                DeviceId = Guid.NewGuid().ToString("N"),
                MacAddress = mac,
                IpAddress = ip,
                Hostname = observation.Hostname,
                Vendor = observation.Vendor,
                TypeGuess = observation.TypeHint,
                FirstSeenUtc = observedAt,
                LastSeenUtc = observedAt,
                Confidence = 0.2,
                IsOnline = true
            };
            InsertDevice(connection, device);
            InsertEvent(connection, device.DeviceId, "join", observedAt, "first_seen");
        }
        else
        {
            var wasOffline = !device.IsOnline;
            device.MacAddress = mac ?? device.MacAddress;
            device.IpAddress = ip ?? device.IpAddress;
            device.Hostname = observation.Hostname ?? device.Hostname;
            device.Vendor = observation.Vendor ?? device.Vendor;
            device.TypeGuess = observation.TypeHint ?? device.TypeGuess;
            device.LastSeenUtc = observedAt;
            device.Confidence = Math.Min(1.0, device.Confidence + 0.1);
            device.IsOnline = true;

            UpdateDevice(connection, device);

            if (wasOffline)
            {
                InsertEvent(connection, device.DeviceId, "join", observedAt, "back_online");
            }
        }

        ApplyClassification(device, observation);
        InsertObservation(connection, device!.DeviceId, observation, observedAt, mac);

        tx.Commit();
        _eventHub.PublishDevice(device!);
        return device!;
    }

    public int MarkOfflineIfStale(TimeSpan threshold)
    {
        var cutoff = DateTime.UtcNow.Subtract(threshold);
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var tx = connection.BeginTransaction();

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            SELECT device_id, last_seen_utc
            FROM devices
            WHERE is_online = 1 AND last_seen_utc < $cutoff
            """;
        cmd.Parameters.AddWithValue("$cutoff", cutoff.ToString("O"));

        var stale = new List<(string DeviceId, DateTime LastSeenUtc)>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            stale.Add((reader.GetString(0), DateTime.Parse(reader.GetString(1))));
        }

        foreach (var item in stale)
        {
            var update = connection.CreateCommand();
            update.CommandText = """
                UPDATE devices
                SET is_online = 0
                WHERE device_id = $deviceId
                """;
            update.Parameters.AddWithValue("$deviceId", item.DeviceId);
            update.ExecuteNonQuery();

            InsertEvent(connection, item.DeviceId, "leave", DateTime.UtcNow, "stale_timeout");
        }

        tx.Commit();
        return stale.Count;
    }

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS devices (
                device_id TEXT PRIMARY KEY,
                mac_address TEXT,
                ip_address TEXT,
                hostname TEXT,
                vendor TEXT,
                type_guess TEXT,
                first_seen_utc TEXT,
                last_seen_utc TEXT,
                confidence REAL,
                is_online INTEGER
            );
            CREATE INDEX IF NOT EXISTS idx_devices_mac ON devices(mac_address);
            CREATE INDEX IF NOT EXISTS idx_devices_ip ON devices(ip_address);

            CREATE TABLE IF NOT EXISTS observations (
                observation_id INTEGER PRIMARY KEY AUTOINCREMENT,
                device_id TEXT,
                source TEXT,
                mac_address TEXT,
                ip_address TEXT,
                hostname TEXT,
                vendor TEXT,
                type_hint TEXT,
                service_hint TEXT,
                observed_at_utc TEXT
            );

            CREATE TABLE IF NOT EXISTS device_events (
                event_id INTEGER PRIMARY KEY AUTOINCREMENT,
                device_id TEXT,
                event_type TEXT,
                occurred_at_utc TEXT,
                detail TEXT
            );
            """;
        cmd.ExecuteNonQuery();

        try
        {
            var alter = connection.CreateCommand();
            alter.CommandText = "ALTER TABLE observations ADD COLUMN service_hint TEXT";
            alter.ExecuteNonQuery();
        }
        catch (SqliteException)
        {
        }
    }

    private static Device? FindDevice(SqliteConnection connection, string? mac, string? ip)
    {
        Device? device = null;
        if (!string.IsNullOrWhiteSpace(mac))
        {
            device = FindDeviceBy(connection, "mac_address", mac);
        }

        if (device is null && !string.IsNullOrWhiteSpace(ip))
        {
            device = FindDeviceBy(connection, "ip_address", ip);
        }

        return device;
    }

    private static Device? FindDeviceBy(SqliteConnection connection, string column, string value)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            SELECT device_id, mac_address, ip_address, hostname, vendor, type_guess,
                   first_seen_utc, last_seen_utc, confidence, is_online
            FROM devices
            WHERE {column} = $value
            LIMIT 1
            """;
        cmd.Parameters.AddWithValue("$value", value);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return MapDevice(reader);
        }

        return null;
    }

    private static Device MapDevice(SqliteDataReader reader)
    {
        return new Device
        {
            DeviceId = reader.GetString(0),
            MacAddress = reader.IsDBNull(1) ? null : reader.GetString(1),
            IpAddress = reader.IsDBNull(2) ? null : reader.GetString(2),
            Hostname = reader.IsDBNull(3) ? null : reader.GetString(3),
            Vendor = reader.IsDBNull(4) ? null : reader.GetString(4),
            TypeGuess = reader.IsDBNull(5) ? null : reader.GetString(5),
            FirstSeenUtc = DateTime.Parse(reader.GetString(6)),
            LastSeenUtc = DateTime.Parse(reader.GetString(7)),
            Confidence = reader.GetDouble(8),
            IsOnline = reader.GetInt32(9) == 1
        };
    }

    private static void InsertDevice(SqliteConnection connection, Device device)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO devices (
                device_id, mac_address, ip_address, hostname, vendor, type_guess,
                first_seen_utc, last_seen_utc, confidence, is_online
            )
            VALUES (
                $deviceId, $mac, $ip, $hostname, $vendor, $typeGuess,
                $firstSeen, $lastSeen, $confidence, $isOnline
            )
            """;
        cmd.Parameters.AddWithValue("$deviceId", device.DeviceId);
        cmd.Parameters.AddWithValue("$mac", (object?)device.MacAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ip", (object?)device.IpAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$hostname", (object?)device.Hostname ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$vendor", (object?)device.Vendor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$typeGuess", (object?)device.TypeGuess ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$firstSeen", device.FirstSeenUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$lastSeen", device.LastSeenUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$confidence", device.Confidence);
        cmd.Parameters.AddWithValue("$isOnline", device.IsOnline ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    private static void UpdateDevice(SqliteConnection connection, Device device)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            UPDATE devices
            SET mac_address = $mac,
                ip_address = $ip,
                hostname = $hostname,
                vendor = $vendor,
                type_guess = $typeGuess,
                last_seen_utc = $lastSeen,
                confidence = $confidence,
                is_online = $isOnline
            WHERE device_id = $deviceId
            """;
        cmd.Parameters.AddWithValue("$deviceId", device.DeviceId);
        cmd.Parameters.AddWithValue("$mac", (object?)device.MacAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ip", (object?)device.IpAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$hostname", (object?)device.Hostname ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$vendor", (object?)device.Vendor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$typeGuess", (object?)device.TypeGuess ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$lastSeen", device.LastSeenUtc.ToString("O"));
        cmd.Parameters.AddWithValue("$confidence", device.Confidence);
        cmd.Parameters.AddWithValue("$isOnline", device.IsOnline ? 1 : 0);
        cmd.ExecuteNonQuery();
    }

    private static void InsertObservation(SqliteConnection connection, string deviceId, Observation observation, DateTime observedAt, string? mac)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO observations (
                device_id, source, mac_address, ip_address, hostname, vendor, type_hint, service_hint, observed_at_utc
            )
            VALUES (
                $deviceId, $source, $mac, $ip, $hostname, $vendor, $typeHint, $serviceHint, $observedAt
            )
            """;
        cmd.Parameters.AddWithValue("$deviceId", deviceId);
        cmd.Parameters.AddWithValue("$source", observation.Source);
        cmd.Parameters.AddWithValue("$mac", (object?)mac ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$ip", (object?)observation.IpAddress ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$hostname", (object?)observation.Hostname ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$vendor", (object?)observation.Vendor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$typeHint", (object?)observation.TypeHint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$serviceHint", (object?)observation.ServiceHint ?? DBNull.Value);
        cmd.Parameters.AddWithValue("$observedAt", observedAt.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    private void InsertEvent(SqliteConnection connection, string deviceId, string eventType, DateTime occurredAt, string? detail)
    {
        var cmd = connection.CreateCommand();
        cmd.CommandText = """
            INSERT INTO device_events (device_id, event_type, occurred_at_utc, detail)
            VALUES ($deviceId, $eventType, $occurredAt, $detail)
            """;
        cmd.Parameters.AddWithValue("$deviceId", deviceId);
        cmd.Parameters.AddWithValue("$eventType", eventType);
        cmd.Parameters.AddWithValue("$occurredAt", occurredAt.ToString("O"));
        cmd.Parameters.AddWithValue("$detail", (object?)detail ?? DBNull.Value);
        cmd.ExecuteNonQuery();

        _eventHub.PublishEvent(new DeviceEvent
        {
            DeviceId = deviceId,
            EventType = eventType,
            OccurredAtUtc = occurredAt,
            Detail = detail
        });
    }

    private static string? NormalizeMac(string? mac)
    {
        return string.IsNullOrWhiteSpace(mac) ? null : mac.Trim().ToLowerInvariant();
    }

    private void ApplyClassification(Device device, Observation observation)
    {
        var result = _classifier.Classify(device, observation);
        if (!string.IsNullOrWhiteSpace(result.Vendor) && string.IsNullOrWhiteSpace(device.Vendor))
        {
            device.Vendor = result.Vendor;
        }
        if (!string.IsNullOrWhiteSpace(result.TypeGuess))
        {
            device.TypeGuess = result.TypeGuess;
        }
        if (result.Confidence > device.Confidence)
        {
            device.Confidence = result.Confidence;
        }
    }
}
