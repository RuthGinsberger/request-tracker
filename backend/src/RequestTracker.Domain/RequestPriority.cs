namespace RequestTracker.Domain;

/// <summary>Request priority. Numeric order equals business order (Low &lt; Medium &lt; High).</summary>
public enum RequestPriority : byte
{
    Low = 1,
    Medium = 2,
    High = 3,
}
