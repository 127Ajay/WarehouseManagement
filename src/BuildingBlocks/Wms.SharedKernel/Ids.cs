namespace Wms.SharedKernel;

/// <summary>
/// All IDs are UUID v7: time ordered, so B-tree inserts stay cheap (LLD 2.1).
/// Use this instead of Guid.NewGuid().
/// </summary>
public static class Ids
{
    public static Guid New() => Guid.CreateVersion7();
}
