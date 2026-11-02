namespace AttributeSystem;

/// <summary>
/// 运行时属性。base 属性的值可直接读写（写入时钳制到 [MinValue, MaxValue]）；
/// derived 属性的值只读，缓存-懒加载：失效后首次读取时经统一求值委托重算并缓存。
/// 最值等定义数据不复制，经由持有的出生 spec 引用提供（单一真相源）。
/// </summary>
public sealed class Attribute
{
    private readonly AttributeSpec _spec;
    private AttributeValueType _value;
    private bool _hasCachedValue;
    private Func<AttributeValueType>? _evaluator;

    internal AttributeOwner? Owner { get; set; }

    /// <summary>属性名（AttributeName），owner 内唯一。</summary>
    public string Name { get; }

    /// <summary>是否为 derived 属性。</summary>
    public bool IsDerived { get; }

    /// <summary>是否处于脏状态（仅 derived 有意义）：缓存为空，即尚未计算过、或已失效且尚未重算。
    /// 该查询支撑 owner 失效 BFS 的剪枝不变量——若 derived 为脏，则其全部传递下游亦为脏
    /// （失效波按完整闭包标脏、读取只洗净被读节点的上游闭包，归纳可得）。</summary>
    internal bool IsDirty => IsDerived && !_hasCachedValue;

    /// <summary>下限，经由出生 spec 提供。</summary>
    public AttributeValueType MinValue => _spec.MinValue;

    /// <summary>上限，经由出生 spec 提供。</summary>
    public AttributeValueType MaxValue => _spec.MaxValue;

    /// <summary>值变化/失效事件，携带发生变化的属性与旧值；新值经由该属性读取获得
    /// （derived 惰性求值——读取时才触发重算，未读取不产生求值）。
    /// 分发按订阅快照进行：处理器内的订阅/退订不影响当次分发。</summary>
    public event Action<Attribute, AttributeValueType?>? ValueChanged;

    private Attribute(AttributeSpec spec, bool isDerived, AttributeValueType initialValue)
    {
        _spec = spec;
        Name = spec.AttributeName;
        IsDerived = isDerived;
        _value = NumberOps<AttributeValueType>.Clamp(initialValue, MinValue, MaxValue);
    }

    internal static Attribute CreateBase(AttributeSpec spec, AttributeValueType initialValue)
        => new(spec, isDerived: false, initialValue);

    internal static Attribute CreateBase(AttributeSpec spec)
        => new(spec, isDerived: false, default);

    internal static Attribute CreateDerived(AttributeSpec spec)
        => new(spec, isDerived: true, default);

    /// <summary>克隆：同一出生 spec 与 base/derived 身份；base 继承当前值（含构建时声明的初值），
    /// derived 全新（缓存空，读取时按克隆自身的 base 值重算）；求值器/事件订阅不继承，
    /// 由 owner 装配时重新绑定。</summary>
    internal Attribute CloneAttribute()
        => IsDerived ? CreateDerived(_spec) : CreateBase(_spec, _value);

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
            // 幂等写入判定必须精确比较两侧的已存储值（均为钳制后的确定值），引入容差会吞掉合法的
            // 微增量更新、造成静默丢值——S1244 在此为误报
            if (clamped == _value) // NOSONAR S1244
                return;
            var oldValue = _value;
            _value = clamped;
            ValueChanged?.Invoke(this, oldValue);
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

    /// <summary>owner 失效时调用：清空缓存并返回失效前的缓存快照（正常必有缓存值；
    /// 从未计算过则返回 null，该防御性分支在新通知语义下不可达）。</summary>
    internal AttributeValueType? TakeInvalidationSnapshot()
    {
        var snapshot = _hasCachedValue ? _value : (AttributeValueType?)null;
        _hasCachedValue = false;
        return snapshot;
    }

    /// <summary>发出失效事件，携带失效前的缓存快照作为旧值；新值由订阅方读取该属性时惰性重算。</summary>
    internal void RaiseInvalidated(AttributeValueType? oldSnapshot)
    {
        ValueChanged?.Invoke(this, oldSnapshot);
    }
}
