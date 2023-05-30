using Google.Protobuf;

namespace System;

public static class TimeConversion
{
    public static ByteString ToRFC3339(this DateTime dateTime)
    {
        if (dateTime.Kind == DateTimeKind.Unspecified) // assume UTC
            dateTime = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

        if (dateTime.Kind == DateTimeKind.Local)
            dateTime = dateTime.ToUniversalTime();

        return ByteString.CopyFromUtf8(dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
    }

    public static ByteString ToRFC3339(this DateTimeOffset dateTime) =>
        dateTime.UtcDateTime.ToRFC3339();

    public static ByteString ToRFC3339(this DateOnly date) =>
        ByteString.CopyFromUtf8(date.ToString("yyyy-MM-ddT00:00:00.000Z"));

    public static ByteString ToRFC3339(this TimeOnly time) =>
        ByteString.CopyFromUtf8(time.ToString("HH:mm:ss.fffZ"));

    public static ByteString ToRFC3339(this TimeSpan time) =>
        ByteString.CopyFromUtf8(time.ToString("HH:mm:ss.fffZ"));
}
