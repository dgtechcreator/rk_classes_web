namespace SchoolMS.Domain;

/// <summary>A physical room lectures are held in (Room 101, Lab, Hall ...). The admin keeps the list; a lecture picks one.</summary>
public class Classroom
{
    public int      ClassroomId { get; set; }
    public string   Name        { get; set; } = "";
    public int?     Capacity    { get; set; }
    /// <summary>Floor / building / wing - free text.</summary>
    public string?  Location    { get; set; }
    public bool     IsActive    { get; set; } = true;
}

/// <summary>One room on the live board: free or engaged right now, what is on, what comes next.</summary>
public class ClassroomStatus
{
    public Classroom Room { get; set; } = new();
    /// <summary>"busy" (a lecture is on now) or "free".</summary>
    public string    State { get; set; } = "free";
    public Lecture?  Current { get; set; }
    /// <summary>Busy: when the room becomes free (back-to-back lectures are chained).</summary>
    public TimeSpan? BusyUntil { get; set; }
    /// <summary>The next lecture still to come today.</summary>
    public Lecture?  Next { get; set; }
    /// <summary>Every (non-cancelled) lecture of the room on the chosen day, in time order.</summary>
    public List<Lecture> Day { get; set; } = new();
}
