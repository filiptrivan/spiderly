using System.Diagnostics;

namespace Spiderly.Shared.Tests
{
    // Shared helper for tests that assert the no-ambient-Activity fallback paths, so "how to reach a
    // no-trace state" lives in one place instead of being inlined per test. Test hosts/runners may carry
    // an ambient Activity; unwinding the Current chain is the reliable way to clear it.
    internal static class TestActivities
    {
        public static void StopAmbient()
        {
            while (Activity.Current != null)
                Activity.Current.Stop();
        }
    }
}
