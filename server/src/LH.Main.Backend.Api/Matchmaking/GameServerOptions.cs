namespace LH.Main.Backend.Api.Matchmaking;

public sealed class GameServerOptions
{
    public string? SharedKey { get; set; }

    public int TicketLifetimeSeconds { get; set; }

    public List<GameServerSlotOptions> Slots { get; set; } = [];

    public TimeSpan GetTicketLifetime() => TimeSpan.FromSeconds(TicketLifetimeSeconds);

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(SharedKey)) errors.Add("GameServers:SharedKey is required.");
        if (TicketLifetimeSeconds <= 0) errors.Add("GameServers:TicketLifetimeSeconds must be greater than zero.");
        if (Slots.Count == 0) errors.Add("At least one GameServers:Slots entry is required.");

        foreach (var slot in Slots)
        {
            if (string.IsNullOrWhiteSpace(slot.ServerId)) errors.Add("GameServers:Slots entries require ServerId.");
            if (string.IsNullOrWhiteSpace(slot.PublicHost)) errors.Add($"GameServers:Slots:{slot.ServerId}:PublicHost is required.");
            if (slot.PublicPort is <= 0 or > 65535) errors.Add($"GameServers:Slots:{slot.ServerId}:PublicPort must be from 1 to 65535.");
        }

        return errors;
    }
}

public sealed class GameServerSlotOptions
{
    public string? ServerId { get; set; }

    public string? PublicHost { get; set; }

    public int PublicPort { get; set; }
}
