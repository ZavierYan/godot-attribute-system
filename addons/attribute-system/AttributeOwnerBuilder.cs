namespace AttributeSystem;

/// <summary>
/// 属性容器构建器：传入所需 SpecName 集合，从 <see cref="AttributeSpecLibrary"/> 取定义，
/// 以各 spec 的 AttributeName 为属性名构建 <see cref="AttributeOwner"/>。
/// 构建期统一完成校验：缺失 spec、表达式语法错误、表达式未知引用、同次构建属性名重复、循环依赖。
/// </summary>
public sealed class AttributeOwnerBuilder
{
    private readonly AttributeSpecLibrary _library;
    private readonly List<string> _requiredSpecNames = new();
    private readonly HashSet<string> _requiredSpecNameSet = new(AttributeNames.Comparer);

    public AttributeOwnerBuilder(AttributeSpecLibrary library)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
    }

    /// <summary>按 SpecName 请求一个属性定义。</summary>
    public AttributeOwnerBuilder Require(string specName)
    {
        Guard.NotBlank(specName, nameof(specName));
        if (_requiredSpecNameSet.Add(specName))
            _requiredSpecNames.Add(specName);
        return this;
    }

    /// <summary>按 SpecName 批量请求属性定义。</summary>
    public AttributeOwnerBuilder Require(IEnumerable<string> specNames)
    {
        foreach (var specName in specNames)
            Require(specName);
        return this;
    }

    /// <summary>执行构建。所有构建期错误在本方法中抛出。</summary>
    public AttributeOwner Build()
    {
        if (_requiredSpecNames.Count == 0)
            throw new InvalidOperationException("构建前至少 Require 一个 SpecName。");

        // 1. 按 SpecName 取定义（缺失 spec 在此暴露）
        var specs = new List<AttributeSpec>(_requiredSpecNames.Count);
        foreach (var specName in _requiredSpecNames)
            specs.Add(_library.Get(specName));

        // 2. 汇总属性名；同次构建 AttributeName 重复即歧义（表达式按属性名解析），拒绝
        var specByAttributeName = new Dictionary<string, AttributeSpec>(AttributeNames.Comparer);
        foreach (var spec in specs)
        {
            if (specByAttributeName.TryGetValue(spec.AttributeName, out var conflict))
                throw new InvalidOperationException(
                    $"同一次构建中属性名 '{spec.AttributeName}' 重复（SpecName '{conflict.SpecName}' 与 '{spec.SpecName}'）；" +
                    "共享 AttributeName 的 spec 只能分属不同的 owner 构建。");
            specByAttributeName.Add(spec.AttributeName, spec);
        }

        // 3. 逐个 derived 将表达式编译为统一求值委托并注入属性（构建期装配）：
        //    语法错误、未知引用在此暴露；表达式同时声明依赖关系，失效图由其标识符决定
        var calculations = new Dictionary<string, DerivedCalculation>(AttributeNames.Comparer);
        foreach (var spec in specs)
        {
            if (!spec.IsDerived)
                continue;
            calculations[spec.AttributeName] = ValidateSpecExpression(spec, specByAttributeName);
        }

        // 4. 建 derived→derived 依赖图：DFS 拓扑排序 + 环检测（报出环路属性链）
        var topoIndex = BuildTopologicalOrder(calculations);

        // 5. 实例化属性与 owner，绑定求值器（依赖值经 owner 惰性解析）
        var attributes = specs
            .Select(spec => spec.IsDerived
                ? Attribute.CreateDerived(spec)
                : Attribute.CreateBase(spec))
            .ToList();

        var directDependents = new Dictionary<string, List<string>>(AttributeNames.Comparer);
        foreach (var (attributeName, calculation) in calculations)
        {
            foreach (var dep in calculation.Dependencies)
            {
                if (!directDependents.TryGetValue(dep, out var list))
                    directDependents[dep] = list = new List<string>();
                list.Add(attributeName);
            }
        }

        var owner = new AttributeOwner(attributes, directDependents, topoIndex);
        foreach (var (attributeName, calculation) in calculations)
        {
            var deps = calculation.Dependencies;
            owner[attributeName].BindEvaluator(() => calculation.Calculator(owner.GetDependencyValues(deps)));
        }
        return owner;
    }

    /// <summary>一条 derived 属性的求值方案：依赖集合与统一求值委托。</summary>
    private sealed record DerivedCalculation(IReadOnlyList<string> Dependencies, AttributeCalculator Calculator);

    /// <summary>解析并校验 spec 表达式（语法错误、未知引用在此暴露），返回其求值方案。</summary>
    private static DerivedCalculation ValidateSpecExpression(
        AttributeSpec spec, Dictionary<string, AttributeSpec> specByAttributeName)
    {
        var compiled = new CompiledExpression(spec.Expression!);
        foreach (var dep in compiled.Dependencies)
        {
            if (!specByAttributeName.ContainsKey(dep))
                throw new InvalidOperationException(
                    $"spec '{spec.SpecName}'（属性 '{spec.AttributeName}'）的表达式 " +
                    $"'{compiled.ExpressionText}' 引用了本次构建未包含的属性：'{dep}'。");
        }
        return new DerivedCalculation(compiled.Dependencies.ToList(), compiled.AsCalculator());
    }

    private static Dictionary<string, int> BuildTopologicalOrder(
        Dictionary<string, DerivedCalculation> calculations)
    {
        var order = new List<string>(calculations.Count);
        var state = new Dictionary<string, int>(AttributeNames.Comparer); // 0/缺失=未访问 1=访问中 2=完成
        var path = new List<string>();

        foreach (var name in calculations.Keys)
            Visit(name);

        var topoIndex = new Dictionary<string, int>(order.Count, AttributeNames.Comparer);
        for (var i = 0; i < order.Count; i++)
            topoIndex[order[i]] = i;
        return topoIndex;

        void Visit(string name)
        {
            switch (state.GetValueOrDefault(name))
            {
                case 2:
                    return;
                case 1:
                {
                    var cycleStart = path.IndexOf(name);
                    var cycle = path.Skip(cycleStart).Append(name);
                    throw new InvalidOperationException(
                        "检测到循环依赖：" + string.Join(" -> ", cycle) + "。");
                }
            }
            state[name] = 1;
            path.Add(name);
            foreach (var dep in calculations[name].Dependencies)
            {
                if (calculations.ContainsKey(dep))
                    Visit(dep);
            }
            path.RemoveAt(path.Count - 1);
            state[name] = 2;
            order.Add(name);
        }
    }
}
