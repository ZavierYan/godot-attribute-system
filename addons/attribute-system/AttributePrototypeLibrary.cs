namespace AttributeSystem;

/// <summary>
/// 属性原型库：以原型名为键登记 <see cref="AttributeOwner"/> 原型并供按名克隆。
/// 登记时在库内执行构建并独占原型引用（原型不出库）——把原型误当运行实例改值、
/// 订阅的错误不可表达，builder 的一切构建期错误在登记时暴露而非留到首次克隆时。
/// 原型名库内唯一，同名登记被拒绝。实例化使用而非静态全局，便于多套模板共存与测试隔离。
/// </summary>
public sealed class AttributePrototypeLibrary
{
    private readonly Dictionary<string, AttributeOwner> _prototypes = new(AttributeNames.Comparer);

    /// <summary>登记一个原型：在库内执行构建并以原型名索引保存产物。
    /// 原型名重复时抛出异常；空白原型名即时抛出；builder 为 null 时抛出 ArgumentNullException；
    /// builder 的一切构建期错误在此暴露。</summary>
    public void Register(string prototypeName, AttributeOwnerBuilder builder)
    {
        Guard.NotBlank(prototypeName, nameof(prototypeName));
        ArgumentNullException.ThrowIfNull(builder);
        if (_prototypes.ContainsKey(prototypeName))
            throw new InvalidOperationException(
                $"已存在原型名为 '{prototypeName}' 的原型，不可重复登记。");
        _prototypes.Add(prototypeName, builder.Build());
    }

    /// <summary>按原型名克隆出新实例；未登记时抛出异常，错误信息包含该原型名。</summary>
    public AttributeOwner Clone(string prototypeName)
    {
        if (!_prototypes.TryGetValue(prototypeName, out var prototype))
            throw new KeyNotFoundException($"原型库中不存在名为 '{prototypeName}' 的原型。");
        return prototype.Clone();
    }

    /// <summary>按原型名尝试克隆出新实例；未登记时返回 false 而不抛出异常。</summary>
    public bool TryClone(string prototypeName, out AttributeOwner owner)
    {
        if (_prototypes.TryGetValue(prototypeName, out var prototype))
        {
            owner = prototype.Clone();
            return true;
        }
        owner = null!;
        return false;
    }

    /// <summary>判断是否存在指定原型名的原型。</summary>
    public bool Contains(string prototypeName) => _prototypes.ContainsKey(prototypeName);

    /// <summary>全部已登记的原型名（快照，当次枚举不受后续登记影响）。</summary>
    public IReadOnlyCollection<string> PrototypeNames() => _prototypes.Keys.ToArray();
}
