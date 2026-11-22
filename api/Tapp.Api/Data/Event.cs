namespace Tapp.Api.Data;

// A party or service session. Orders belong to the running event.
public class Event
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    // Mirrors "EndedAt is null" so a filtered unique index can allow only one running event.
    public bool IsRunning { get; set; }
}
