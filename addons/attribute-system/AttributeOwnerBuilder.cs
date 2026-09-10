namespace AttributeSystem;

/// <summary>
/// 属性容器构建器：传入所需属性名集合，从 <see cref="AttributeSpecLibrary"/> 取定义，
/// 以 <see cref="Require(string, string)"/> 声明的表达式派生计算（表达式同时声明计算方法与依赖关系），
/// 构建出 <see cref="AttributeOwner"/>。
/// 构建期统一完成校验：缺失 spec、同名冲突声明、表达式语法错误、表达式未知引用、循环依赖。
/// </summary>
public sealed class AttributeOwnerBuilder
{
    private readonly AttributeSpecLibrary _library;
    private readonly List<string> _requiredAttributeNames = new();
    private readonly HashSet<string> _requiredAttributeNameSet = new(AttributeNames.Comparer);
    private readonly Dictionary<string, string> _expressionsByAttributeName = new(AttributeNames.Comparer);
    private readonly HashSet<string> _conflictedNames = new(AttributeNames.Comparer);

    public AttributeOwnerBuilder(AttributeSpecLibrary library)
    {
        _library = library ?? throw new ArgumentNullException(nameof(library));
    }

    /// <summary>声明一个 base 成员：属性定义经库按名取得，值可直接读写。</summary>
    public AttributeOwnerBuilder Require(string attributeName)
    {
        Guard.NotBlank(attributeName, nameof(attributeName));
        AddRequired(attributeName, expression: null);
        return this;
    }

    /// <summary>声明一个 derived 成员：成员资格与派生计算一次声明，表达式同时声明计算方法与依赖关系。
    /// 表达式空白在声明调用时即时抛出；同名冲突声明在 Build 时与其它构建期校验一并暴露。</summary>
    public AttributeOwnerBuilder Require(string attributeName, string expression)
    {
        Guard.NotBlank(attributeName, nameof(attributeName));
        Guard.NotBlank(expression, nameof(expression));
        AddRequired(attributeName, expression);
        return this;
    }

    /// <summary>按属性名批量声明 base 成员。</summary>
    public AttributeOwnerBuilder Require(IEnumerable<string> attributeNames)
    {
        ArgumentNullException.ThrowIfNull(attributeNames);
        foreach (var attributeName in attributeNames)
            Require(attributeName);
        return this;
    }

    /// <summary>执行构建。所有构建期错误在本方法中抛出。</summary>
    public AttributeOwner Build()
    {
        if (_requiredAttributeNames.Count == 0)
            throw new InvalidOperationException("构建前至少 Require 一个属性名。");

        if (_conflictedNames.Count > 0)
            throw new InvalidOperationException(
                "以下属性名存在冲突的重复声明（base/derived 混合，或重复声明表达式）：" +
                string.Join("、", _conflictedNames) + "。");

        // 1. 按属性名取定义（缺失 spec 在此暴露）
        var specByAttributeName = new Dictionary<string, AttributeSpec>(
            _requiredAttributeNames.Count, AttributeNames.Comparer);
        foreach (var attributeName in _requiredAttributeNames)
            specByAttributeName[attributeName] = _library.Get(attributeName);

        // 2. 逐个编译声明的表达式（构建期装配）：语法错误、未知引用在此暴露；
        //    表达式同时声明依赖关系，失效图由其标识符决定
        var calculations = new Dictionary<string, DerivedCalculation>(
            _expressionsByAttributeName.Count, AttributeNames.Comparer);
        foreach (var (attributeName, expression) in _expressionsByAttributeName)
            calculations[attributeName] = CompileExpression(attributeName, expression, specByAttributeName);

        // 3. 建 derived→derived 依赖图：DFS 拓扑排序 + 环检测（报出环路属性链）
        var topoIndex = BuildTopologicalOrder(calculations);

        // 4. 实例化属性、建反向依赖表，交 owner 装配（求值器绑定在 owner 构造内完成）
        var attributes = specByAttributeName.Values
            .Select(spec => calculations.ContainsKey(spec.AttributeName)
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

        return new AttributeOwner(attributes, calculations, directDependents, topoIndex);
    }

    /// <summary>编译并校验声明的表达式（语法错误、未知引用在此暴露），返回其求值方案。</summary>
    private static DerivedCalculation CompileExpression(
        string attributeName, string expression,
        Dictionary<string, AttributeSpec> specByAttributeName)
    {
        var compiled = new CompiledExpression(expression);
        foreach (var dep in compiled.Dependencies)
        {
            if (!specByAttributeName.ContainsKey(dep))
                throw new InvalidOperationException(
                    $"属性 '{attributeName}' 声明的表达式 '{compiled.ExpressionText}' " +
                    $"引用了本次构建未包含的属性：'{dep}'。");
        }
        return new DerivedCalculation(compiled.Dependencies.ToList(), compiled.AsCalculator());
    }

    /// <summary>登记一次成员声明：新名入列；重复 base 声明幂等；
    /// base/derived 混合或重复表达式声明记为冲突，Build 时统一报错。</summary>
    private void AddRequired(string attributeName, string? expression)
    {
        if (!_requiredAttributeNameSet.Add(attributeName))
        {
            if (_expressionsByAttributeName.ContainsKey(attributeName) || expression is not null)
                _conflictedNames.Add(attributeName);
            return;
        }
        _requiredAttributeNames.Add(attributeName);
        if (expression is not null)
            _expressionsByAttributeName[attributeName] = expression;
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
