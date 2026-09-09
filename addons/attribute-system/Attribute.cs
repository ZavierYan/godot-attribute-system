namespace AttributeSystem;

/// <summary>
/// 属性值变更事件参数。
/// <para>base 变更事件：OldValue 为旧值，NewValue 直接可得。</para>
/// <para>derived 失效事件：OldValue 为失效前的缓存快照（从未计算过则为 null）；
/// NewValue 以惰性方式暴露——仅当订阅方访问时才触发重算，未访问不产生求值。</para>
/// </summary>
public sealed class AttributeChangedEventArgs : EventArgs
{
    private readonly Func<AttributeValueType>? _newValueProvider;
    private AttributeValueType? _newValue;

    /// <summary>发生变化的属性名（AttributeName）。</summary>
    public string Name { get; }

    /// <summary>旧值；derived 从未计算过时为 null。</summary>
    public AttributeValueType? OldValue { get; }

    /// <summary>新值。derived 失效事件中首次访问时触发重算（重算结果同时写入缓存，多次访问至多重算一次）。</summary>
    public AttributeValueType NewValue
    {
        get
        {
            if (_newValue.HasValue)
                return _newValue.Value;
            if (_newValueProvider is null)
                throw new InvalidOperationException("该事件未携带新值。");
            var value = _newValueProvider();
            _newValue = value;
            return value;
        }
    }

    internal AttributeChangedEventArgs(string name, AttributeValueType? oldValue, AttributeValueType newValue)
    {
        Name = name;
        OldValue = oldValue;
        _newValue = newValue;
    }

    internal AttributeChangedEventArgs(string name, AttributeValueType? oldValue, Func<AttributeValueType> newValueProvider)
    {
        Name = name;
        OldValue = oldValue;
        _newValueProvider = newValueProvider;
    }
}

/// <summary>
/// 运行时属性。base 属性的值可直接读写（写入时钳制到 [MinValue, MaxValue]）；
/// derived 属性的值只读，缓存-懒加载：失效后首次读取时经统一求值委托重算并缓存。
/// </summary>
public sealed class Attribute
{
    private AttributeValueType _value;
    private bool _hasCachedValue;
    private Func<AttributeValueType>? _evaluator;

    internal AttributeOwner? Owner { get; set; }

    /// <summary>属性名（AttributeName），owner 内唯一。</summary>
    public string Name { get; }

    /// <summary>来源 spec 的 SpecName，只读调试信息，不参与运行时行为。</summary>
    public string SourceSpecName { get; }

    /// <summary>是否为 derived 属性。</summary>
    public bool IsDerived { get; }

    public AttributeValueType MinValue { get; }

    public AttributeValueType MaxValue { get; }

    /// <summary>值变化/失效事件。分发按订阅快照进行：处理器内的订阅/退订不影响当次分发。</summary>
    public event EventHandler<AttributeChangedEventArgs>? ValueChanged;

    private Attribute(AttributeSpec spec, bool isDerived)
    {
        Name = spec.AttributeName;
        SourceSpecName = spec.SpecName;
        MinValue = spec.MinValue;
        MaxValue = spec.MaxValue;
        IsDerived = isDerived;
        _value = NumberOps<AttributeValueType>.Clamp(default, MinValue, MaxValue);
    }

    internal static Attribute CreateBase(AttributeSpec spec) => new(spec, isDerived: false);

    internal static Attribute CreateDerived(AttributeSpec spec) => new(spec, isDerived: true);

    /// <summary>
    /// 当前值（derived 属性读取时惰性求值并缓存）；写入仅限 base 属性。
    /// 写入值先钳制到 [MinValue, MaxValue]；与当前值相同则不产生任何事件，
    /// 否则更新值、发出自身变更事件，并由 owner 沿依赖图按拓扑序失效所有受影响的 derived 属性。
    /// </summary>
    public AttributeValueType Value
    {
        get => IsDerived ? ComputeValue() : _value;
        set
        {
            if (IsDerived)
                throw new InvalidOperationException($"属性 '{Name}' 为 derived，不可直接写入；其值只能随依赖变化而改变。");
            var clamped = NumberOps<AttributeValueType>.Clamp(value, MinValue, MaxValue);
            if (clamped == _value)
                return;
            var oldValue = _value;
            _value = clamped;
            ValueChanged?.Invoke(this, new AttributeChangedEventArgs(Name, oldValue, clamped));
            Owner?.InvalidateDependents(this);
        }
    }

    private AttributeValueType ComputeValue()
    {
        if (_hasCachedValue)
            return _value;
        var evaluator = _evaluator
            ?? throw new InvalidOperationException($"derived 属性 '{Name}' 尚未绑定求值器。");
        var clamped = NumberOps<AttributeValueType>.Clamp(evaluator(), MinValue, MaxValue);
        _value = clamped;
        _hasCachedValue = true;
        return clamped;
    }

    internal void BindEvaluator(Func<AttributeValueType> evaluator)
    {
        if (!IsDerived)
            throw new InvalidOperationException($"属性 '{Name}' 不是 derived，不可绑定求值器。");
        _evaluator = evaluator;
    }

    /// <summary>owner 失效时调用：清空缓存并返回失效前的缓存快照（从未计算过则为 null）。</summary>
    internal AttributeValueType? TakeInvalidationSnapshot()
    {
        var snapshot = _hasCachedValue ? _value : (AttributeValueType?)null;
        _hasCachedValue = false;
        return snapshot;
    }

    /// <summary>发出失效事件。NewValue 惰性：订阅方访问时才经 ComputeValue 重算。</summary>
    internal void RaiseInvalidated(AttributeValueType? oldSnapshot)
    {
        ValueChanged?.Invoke(this, new AttributeChangedEventArgs(Name, oldSnapshot, ComputeValue));
    }
}
