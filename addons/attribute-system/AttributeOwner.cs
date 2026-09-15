namespace AttributeSystem;

/// <summary>
/// 属性容器：持有一次构建得到的全部属性（以 AttributeName 为键），负责 base 写值后的
/// 失效传播——沿反向依赖图收集全部受影响 derived，按拓扑序标记脏并发出失效通知。
/// 构建完成的实例可经 <see cref="Clone"/> 作为原型克隆出同构成的新实例。
/// </summary>
public sealed class AttributeOwner
{
    private readonly Dictionary<string, Attribute> _attributes;
    private readonly Dictionary<string, List<string>> _directDependents;
    private readonly Dictionary<string, int> _topoIndex;
    private readonly Dictionary<string, DerivedCalculation> _calculations;

    /// <summary>builder 装配路径：对外部传入的依赖数据做防御性拷贝，再走统一装配。</summary>
    internal AttributeOwner(
        IEnumerable<Attribute> attributes,
        IReadOnlyDictionary<string, DerivedCalculation> calculations,
        IReadOnlyDictionary<string, List<string>> directDependents,
        IReadOnlyDictionary<string, int> topoIndex)
        : this(
            attributes,
            new Dictionary<string, DerivedCalculation>(calculations, AttributeNames.Comparer),
            new Dictionary<string, List<string>>(directDependents, AttributeNames.Comparer),
            new Dictionary<string, int>(topoIndex, AttributeNames.Comparer))
    {
    }

    /// <summary>
    /// 克隆路径：三张表与表内 List 构建后不再有写路径（约定不可变），与原型直接共享。
    /// </summary>
    private AttributeOwner(
        IEnumerable<Attribute> attributes,
        Dictionary<string, DerivedCalculation> calculations,
        Dictionary<string, List<string>> directDependents,
        Dictionary<string, int> topoIndex)
    {
        _attributes = new Dictionary<string, Attribute>(AttributeNames.Comparer);
        foreach (var attribute in attributes)
        {
            _attributes.Add(attribute.Name, attribute);
            attribute.Owner = this;
        }
        _directDependents = directDependents;
        _topoIndex = topoIndex;
        _calculations = calculations;
        BindCalculations();
    }

    /// <summary>
    /// 以本实例为原型克隆一个同构成的新实例：共享静态构成（出生 spec 引用、编译后的求值委托、
    /// 依赖图与拓扑序），运行时状态：base 属性继承本实例的当前值（构建时声明的初值即原型的初始当前值），
    /// derived 缓存为空、首次读取时按克隆自身的 base 值惰性重算，事件订阅不继承。
    /// 原型与克隆之间、克隆与克隆之间的值与失效传播互不影响；克隆可再作为原型继续克隆。
    /// 注意：同族实例共享编译求值委托（DerivedCalculation 不保留表达式文本，共享是设计强制），
    /// 并发求值不安全——与插件仅主线程使用的约定一致。
    /// </summary>
    public AttributeOwner Clone()
    {
        var attributes = new List<Attribute>(_attributes.Count);
        foreach (var attribute in _attributes.Values)
            attributes.Add(attribute.CloneAttribute());
        return new AttributeOwner(attributes, _calculations, _directDependents, _topoIndex);
    }

    /// <summary>按属性名获取属性。</summary>
    public Attribute this[string attributeName] => Get(attributeName);

    /// <summary>按属性名获取属性；不存在时抛出异常。</summary>
    public Attribute Get(string attributeName)
    {
        if (!_attributes.TryGetValue(attributeName, out var attribute))
            throw new KeyNotFoundException($"AttributeOwner 中不存在名为 '{attributeName}' 的属性。");
        return attribute;
    }

    /// <summary>按属性名尝试获取属性。</summary>
    public bool TryGet(string attributeName, out Attribute attribute)
        => _attributes.TryGetValue(attributeName, out attribute!);

    /// <summary>全部属性。</summary>
    public IEnumerable<Attribute> Attributes => _attributes.Values;

    /// <summary>设置 base 属性的值（等价于 Get(name).Value = value）。</summary>
    public void SetValue(string attributeName, AttributeValueType value) => Get(attributeName).Value = value;

    /// <summary>读取属性当前值（derived 触发惰性求值）。</summary>
    public AttributeValueType GetValue(string attributeName) => Get(attributeName).Value;

    /// <summary>取依赖属性的当前值（derived 依赖递归走同一懒加载路径）。</summary>
    internal IReadOnlyDictionary<string, AttributeValueType> GetDependencyValues(IReadOnlyList<string> names)
    {
        var values = new Dictionary<string, AttributeValueType>(names.Count, AttributeNames.Comparer);
        foreach (var name in names)
            values[name] = _attributes[name].Value;
        return values;
    }

    /// <summary>为每个 derived 绑定求值器：委托闭包捕获本 owner，依赖值经 owner 惰性解析。
    /// builder 装配与克隆共用此路径，保证两条来源的绑定行为一致。</summary>
    private void BindCalculations()
    {
        foreach (var (attributeName, calculation) in _calculations)
        {
            var deps = calculation.Dependencies;
            this[attributeName].BindEvaluator(() => calculation.Calculator(GetDependencyValues(deps)));
        }
    }

    internal void InvalidateDependents(Attribute source)
    {
        if (_directDependents.Count == 0)
            return;

        List<Attribute>? affected = null;
        var visited = new HashSet<string>(AttributeNames.Comparer);
        var queue = new Queue<string>();
        if (_directDependents.TryGetValue(source.Name, out var direct))
            foreach (var name in direct)
                queue.Enqueue(name);

        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            if (!visited.Add(name))
                continue;
            var attribute = _attributes[name];
            if (attribute.IsDerived)
                (affected ??= new List<Attribute>()).Add(attribute);
            if (_directDependents.TryGetValue(name, out var next))
                foreach (var n in next)
                    queue.Enqueue(n);
        }

        if (affected is null)
            return;

        // 阶段一：按拓扑序把全部受影响 derived 标脏并收集失效快照——即使后续通知中有
        // 处理器抛出异常，所有缓存也已失效，后续读取必得重算值，不会残留过期缓存
        var invalidated = affected
            .OrderBy(a => _topoIndex[a.Name])
            .Select(a => (Attribute: a, Snapshot: a.TakeInvalidationSnapshot()))
            .ToList();

        // 阶段二：按拓扑序分发失效通知（处理器抛出的异常向上传播，不在此捕获）
        foreach (var (attribute, snapshot) in invalidated)
            attribute.RaiseInvalidated(snapshot);
    }
}
