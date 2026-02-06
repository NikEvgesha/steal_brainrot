public static class BaseDirtyTracker
{
    private static bool _dirty;

    public static void MarkDirty()
    {
        _dirty = true;
    }

    public static bool Consume()
    {
        if (!_dirty) return false;
        _dirty = false;
        return true;
    }
}
