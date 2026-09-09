namespace AttributeSystem;

/// <summary>Spec 与 builder 共享的参数校验。</summary>
internal static class Guard
{
    /// <summary>校验值不能为空或纯空白字符。</summary>
    public static void NotBlank(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} 不能为空或纯空白字符。", paramName);
    }
}
