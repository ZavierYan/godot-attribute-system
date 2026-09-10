namespace AttributeSystem;

/// <summary>
/// AttributeSpec 库：以 <see cref="AttributeSpec.AttributeName"/> 为键集中登记 spec 并供检索。
/// AttributeName 库内唯一，同名登记被拒绝——属性的身份与约束只有一个定义处，
/// 场景差异（同名属性的不同算法）由不同构建声明不同表达式表达，而非登记多个 spec。
/// 实例化使用而非静态全局，便于多套定义共存与测试隔离。
/// </summary>
public sealed class AttributeSpecLibrary
{
    private readonly Dictionary<string, AttributeSpec> _specs = new(AttributeNames.Comparer);

    /// <summary>登记一个 spec；AttributeName 重复时抛出异常。</summary>
    public void Register(AttributeSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (_specs.ContainsKey(spec.AttributeName))
            throw new InvalidOperationException(
                $"已存在 AttributeName 为 '{spec.AttributeName}' 的 spec，不可重复登记。");
        _specs.Add(spec.AttributeName, spec);
    }

    /// <summary>按 AttributeName 取得 spec；未登记时抛出异常。</summary>
    public AttributeSpec Get(string attributeName)
    {
        if (!_specs.TryGetValue(attributeName, out var spec))
            throw new KeyNotFoundException($"spec 库中不存在 AttributeName 为 '{attributeName}' 的 spec。");
        return spec;
    }

    /// <summary>按 AttributeName 尝试取得 spec。</summary>
    public bool TryGet(string attributeName, out AttributeSpec spec)
        => _specs.TryGetValue(attributeName, out spec!);

    /// <summary>判断是否存在指定 AttributeName 的 spec。</summary>
    public bool Contains(string attributeName) => _specs.ContainsKey(attributeName);
}
