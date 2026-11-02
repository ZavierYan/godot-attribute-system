namespace AttributeSystem;

/// <summary>
/// 聚合栈 derived 的求值方案：seed 表达式 + 三桶类型化贡献，协议固定为
/// (seed + Σ flat) × (1 + Σ additive) × Π(1 + multiplicative)，桶内满足交换律。
/// 与 <see cref="DerivedCalculation"/> 同实现 <see cref="IAttributeCalculation"/>，owner 对两形态无感知。
/// 本类可变：运行期经 <see cref="Add"/>/<see cref="Remove"/> 增撤贡献；
/// <see cref="Dependencies"/> 为增撤时全量重算的并集（贡献数小，不做增量维护）。
/// 三段折叠返回未钳制原始值，钳制由 Attribute.ComputeValue 尾端统一执行。
/// seed 与各贡献的 <see cref="CompiledExpression"/> 均不可变、克隆族之间继续共享；
/// 仅三桶 List 属于本实例状态，可整体复制（见 <see cref="Copy"/>）。
/// </summary>
internal sealed class StackedCalculation : IAttributeCalculation
{
    private readonly CompiledExpression _seed;

    private readonly List<Contribution> _flats = new();
    private readonly List<Contribution> _additives = new();
    private readonly List<Contribution> _multiplicatives = new();

    /// <summary>依赖并集。重算时整体替换为新列表（从不原地修改），拷贝路径可安全共享引用。</summary>
    private IReadOnlyList<string> _dependencies;

    public AttributeCalculator Calculator { get; }

    /// <summary>以 seed 表达式构造无贡献的聚合栈求值方案。</summary>
    internal StackedCalculation(CompiledExpression seed)
    {
        _seed = seed;
        _dependencies = seed.Dependencies;
        Calculator = Compute;
    }

    /// <inheritdoc cref="IAttributeCalculation.Dependencies"/>
    public IReadOnlyList<string> Dependencies => _dependencies;

    /// <summary>私有贡献条目：类型、表达式与签发句柄三元组。不可变；
    /// <see cref="ContributionHandle"/> 对象身份即条目身份，撤销按句柄引用匹配。</summary>
    private sealed record Contribution(ContributionKind Kind, CompiledExpression Expression, ContributionHandle Handle);

    /// <summary>三段折叠求值：seed → 累加 flat → (1 + additive 池和) → Π(1 + multiplicative)。
    /// 接收依赖值全集即可（每个表达式按需取用，多余键无害）；返回未钳制原始值。</summary>
    public AttributeValueType Compute(IReadOnlyDictionary<string, AttributeValueType> dependencyValues)
    {
        AttributeValueType acc = _seed.Evaluate(dependencyValues);

        foreach (var flat in _flats)
            acc += flat.Expression.Evaluate(dependencyValues);

        AttributeValueType one = NumberOps<AttributeValueType>.TypeOne;
        AttributeValueType addSum = NumberOps<AttributeValueType>.TypeZero;
        foreach (var additive in _additives)
            addSum += additive.Expression.Evaluate(dependencyValues);

        AttributeValueType mul = one;
        foreach (var multiplicative in _multiplicatives)
            mul *= one + multiplicative.Expression.Evaluate(dependencyValues);

        return acc * (one + addSum) * mul;
    }

    /// <summary>登记一条贡献到对应桶，并全量重算依赖并集。</summary>
    internal void Add(ContributionKind kind, CompiledExpression expression, ContributionHandle handle)
    {
        GetBucket(kind).Add(new Contribution(kind, expression, handle));
        RecomputeDependencies();
    }

    /// <summary>按句柄对象身份在三桶中查找并移除贡献，成功后全量重算依赖并集。</summary>
    /// <returns>是否找到并移除。</returns>
    internal bool Remove(ContributionHandle handle)
    {
        if (RemoveFrom(_flats, handle) || RemoveFrom(_additives, handle) || RemoveFrom(_multiplicatives, handle))
        {
            RecomputeDependencies();
            return true;
        }

        return false;
    }

    /// <summary>复制本方案供写时复制与克隆路径使用：seed 与各贡献的 <see cref="CompiledExpression"/>、
    /// 贡献条目对象、句柄与依赖并集列表均继续共享（不可变），仅三桶 List 深拷贝为独立副本。</summary>
    internal StackedCalculation Copy()
    {
        var copy = new StackedCalculation(_seed);
        copy._flats.AddRange(_flats);
        copy._additives.AddRange(_additives);
        copy._multiplicatives.AddRange(_multiplicatives);
        copy._dependencies = _dependencies;
        return copy;
    }

    private List<Contribution> GetBucket(ContributionKind kind) => kind switch
    {
        ContributionKind.Flat => _flats,
        ContributionKind.Additive => _additives,
        ContributionKind.Multiplicative => _multiplicatives,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "未知的贡献类型。"),
    };

    private static bool RemoveFrom(List<Contribution> bucket, ContributionHandle handle)
    {
        for (var i = 0; i < bucket.Count; i++)
        {
            if (ReferenceEquals(bucket[i].Handle, handle))
            {
                bucket.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    /// <summary>全量重算依赖并集：seed 依赖在前、各贡献按登记序，有序去重（<see cref="AttributeNames.Comparer"/>）。</summary>
    private void RecomputeDependencies()
    {
        var merged = new List<string>();
        var seen = new HashSet<string>(AttributeNames.Comparer);

        // Where(seen.Add)：HashSet.Add 兼任过滤器与去重副作用，有序保留首次出现的依赖名
        merged.AddRange(_seed.Dependencies.Where(seen.Add));

        foreach (var bucket in Buckets())
            foreach (var contribution in bucket)
                merged.AddRange(contribution.Expression.Dependencies.Where(seen.Add));

        _dependencies = merged;
    }

    /// <summary>按协议求值顺序枚举三桶（flat → additive → multiplicative），仅用于遍历。</summary>
    private IEnumerable<List<Contribution>> Buckets()
    {
        yield return _flats;
        yield return _additives;
        yield return _multiplicatives;
    }
}
