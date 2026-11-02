namespace AttributeSystem.Tests;

/// <summary>测试共享辅助。</summary>
internal static class TestHelpers
{
    /// <summary>执行 action 并捕获其抛出的异常；未抛出时返回 null。</summary>
    public static Exception? Catch(Action action)
    {
        try
        {
            action();
            return null;
        }
        catch (Exception e)
        {
            return e;
        }
    }
}
