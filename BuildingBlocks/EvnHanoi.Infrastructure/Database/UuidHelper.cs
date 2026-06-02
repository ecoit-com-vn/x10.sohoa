using System;

namespace EvnHanoi.Infrastructure.Database;

public static class UuidHelper
{
    public static string NewUuid() => Guid.CreateVersion7().ToString();
}
