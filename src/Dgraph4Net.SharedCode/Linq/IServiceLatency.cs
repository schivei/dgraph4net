namespace Dgraph4Net;

public interface IServiceLatency
{
    long ParsingNs { get; }
    long ProcessingNs { get; }
    long EncodingNs { get; }
    long AssignTimestampNs { get; }
    long TotalNs { get; }
    TimeSpan TotalTime => TimeSpan.FromTicks(TotalNs);
}
