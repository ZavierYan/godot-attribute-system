namespace AttributeSystem;

/// <summary>
/// AttributeSpec 库：以 <see cref="AttributeSpec.SpecName"/> 为键集中登记 spec 并供检索。
/// 多个 spec 允许共享同一 <see cref="AttributeSpec.AttributeName"/>；SpecName 重复则拒绝。
/// 实例化使用而非静态全局，便于多套定义共存与测试隔离。
/// </summary>
public sealed class AttributeSpecLibrary
{
    private readonly Dictionary<string, AttributeSpec> _specs = new(AttributeNames.Comparer);

    /// <summary>登记一个 spec；SpecName 重复时抛出异常。</summary>
    public void Register(AttributeSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        if (_specs.ContainsKey(spec.SpecName))
            throw new InvalidOperationException(
                $"已存在 SpecName 为 '{spec.SpecName}' 的 spec，不可重复登记。");
        _specs.Add(spec.SpecName, spec);
    }

    /// <summary>按 SpecName 取得 spec；未登记时抛出异常。</summary>
    public AttributeSpec Get(string specName)
    {
        if (!_specs.TryGetValue(specName, out var spec))
            throw new KeyNotFoundException($"spec 库中不存在 SpecName 为 '{specName}' 的 spec。");
        return spec;
    }

    /// <summary>按 SpecName 尝试取得 spec。</summary>
    public bool TryGet(string specName, out AttributeSpec spec)
        => _specs.TryGetValue(specName, out spec!);

    /// <summary>判断是否存在指定 SpecName 的 spec。</summary>
    public bool Contains(string specName) => _specs.ContainsKey(specName);
}
