namespace Slither.Client;

// Stack-only markers for Android system traces; no allocation per scope.
internal readonly struct ProfileScope : System.IDisposable
{
    public ProfileScope(string name)
    {
#if ANDROID && DEBUG
        global::Android.OS.Trace.BeginSection(name);
#endif
    }

    public void Dispose()
    {
#if ANDROID && DEBUG
        global::Android.OS.Trace.EndSection();
#endif
    }
}
