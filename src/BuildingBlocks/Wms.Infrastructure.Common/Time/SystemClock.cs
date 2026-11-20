using Wms.Application.Abstractions;

namespace Wms.Infrastructure.Common.Time;

/// <summary>The one place allowed to read the system clock (see architecture tests).</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
