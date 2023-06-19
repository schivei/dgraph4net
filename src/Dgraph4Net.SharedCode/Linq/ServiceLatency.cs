namespace Dgraph4Net;

internal readonly record struct ServerLatency(long ParsingNs, long ProcessingNs, long EncodingNs, long AssignTimestampNs, long TotalNs) : IServerLatency;
