namespace AttributeSystem;

/// <summary>
/// 属性容器：持有一次构建得到的全部属性（以 AttributeName 为键），负责 base 写值后的
/// 失效传播——沿反向依赖图收集全部受影响 derived，按拓扑序标记脏并发出失效通知。
/// 构建完成的实例可经 <see cref="Clone"/> 作为原型克隆出同构成的新实例。
/// </summary>
public sealed class AttributeOwner
{
    private readonly Dictionary<string, Attribute> _attributes;
    private Dictionary<string, List<string>> _directDependents;
    private Dictionary<string, int> _topoIndex;
    private Dictionary<string, IAttributeCalculation> _calculations;

    /// <summary>写时复制标志：true 表示本实例的三张静态表（依赖边/拓扑序/计算表）当前属于某个
    /// 共享集、变更前必须先私有化。生命周期：
    /// 构造（builder 与克隆两条路径）时为 true——尚无消费者，但可能立即被 Clone；
    /// <see cref="Clone"/> 时原型与新实例均置为 true——克隆动作使双方的表进入同一共享集
    /// （原型即使此前已私有化，其表从此刻起也被克隆消费）；
    /// 首次变异经 <see cref="EnsureMutableTables"/> COW 私有化后为 false——此后可直接改，
    /// 直到下一次 Clone 使双方重新入集。
    /// 保证：任一实例的变异只落在自己的私有副本上，共享集中的其它实例（含全部克隆）不受波及。</summary>
    private bool _tablesSharedWithClones = true;

    /// <summary>builder 装配路径：对外部传入的依赖数据做防御性拷贝，再走统一装配。</summary>
    internal AttributeOwner(
        IEnumerable<Attribute> attributes,
        IReadOnlyDictionary<string, IAttributeCalculation> calculations,
        IReadOnlyDictionary<string, List<string>> directDependents,
        IReadOnlyDictionary<string, int> topoIndex)
        : this(
            attributes,
            new Dictionary<string, IAttributeCalculation>(calculations, AttributeNames.Comparer),
            new Dictionary<string, List<string>>(directDependents, AttributeNames.Comparer),
            new Dictionary<string, int>(topoIndex, AttributeNames.Comparer))
    {
    }

    /// <summary>
    /// 克隆路径：三张表与表内 List 构建后不再有写路径（约定不可变），与原型直接共享。
    /// </summary>
    private AttributeOwner(
        IEnumerable<Attribute> attributes,
        Dictionary<string, IAttributeCalculation> calculations,
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
    /// 三张静态表与原型直接共享（写时复制前提），经 <see cref="EnsureMutableTables"/> 在首次变异时
    /// 私有化，克隆族之间的变异互不波及。克隆同时把原型自己的共享标志置回 true——
    /// 克隆动作使双方的表进入同一共享集，原型即使此前已私有化，其下一次变异也会重新 COW，
    /// 本克隆持有的构成与值不受影响。
    /// 注意：同族实例共享编译求值委托（求值方案对象在克隆间直接共享，共享是设计强制），
    /// 并发求值不安全——与插件仅主线程使用的约定一致。
    /// </summary>
    public AttributeOwner Clone()
    {
        var attributes = new List<Attribute>(_attributes.Count);
        foreach (var attribute in _attributes.Values)
            attributes.Add(attribute.CloneAttribute());

        // 克隆动作使双方的表进入同一共享集：原型即使此前已私有化，其表从此刻起也被克隆消费，
        // 必须置回共享标志，保证原型下一次变异重新 COW、不波及本克隆
        _tablesSharedWithClones = true;

        return new AttributeOwner(attributes, _calculations, _directDependents, _topoIndex);
    }

    /// <summary>运行时添加一个 base 成员：属性定义来自传入的 spec，值可直接读写。
    /// spec 为 null 或同名属性已存在在本调用即时抛出，owner 状态不变（错误在变异调用时暴露）。</summary>
    public void AddBase(AttributeSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);
        EnsureNameAvailable(spec.AttributeName);
        AddAttributeCore(spec, calculation: null, initialValue: null);
    }

    /// <summary>运行时添加一个携带初值的 base 成员：初值经 base 构造路径即时钳制到 [MinValue, MaxValue]。
    /// spec 为 null 或同名属性已存在在本调用即时抛出，owner 状态不变。</summary>
    public void AddBase(AttributeSpec spec, AttributeValueType initialValue)
    {
        ArgumentNullException.ThrowIfNull(spec);
        EnsureNameAvailable(spec.AttributeName);
        AddAttributeCore(spec, calculation: null, initialValue);
    }

    /// <summary>运行时添加一个 plain derived 成员：表达式同时声明计算方法与依赖关系。
    /// 表达式空白、语法错误、未知引用、同名冲突在本调用即时抛出，owner 状态不变。
    /// 不做环检测：新属性此刻尚未登记，不可能被任何既有表达式的依赖引用，因而依赖边只会
    /// 指向既有名字、不可能成环（自引用会因新名不在依赖存在性检查的范围内被拒绝）。</summary>
    public void AddDerived(AttributeSpec spec, string expression)
    {
        ArgumentNullException.ThrowIfNull(spec);
        EnsureNameAvailable(spec.AttributeName);
        Guard.NotBlank(expression, nameof(expression));
        var compiled = new CompiledExpression(expression);
        ValidateDependencies(spec.AttributeName, compiled);
        AddAttributeCore(
            spec, new DerivedCalculation(compiled.Dependencies.ToList(), compiled.AsCalculator()), initialValue: null);
    }

    /// <summary>运行时添加一个聚合栈 derived 成员：seed 表达式同时声明基值计算与依赖关系，
    /// 初始无贡献、无贡献时的值即 seed 结果的钳制值；贡献经后续原语运行期增撤。
    /// 表达式空白、语法错误、未知引用、同名冲突在本调用即时抛出，owner 状态不变。
    /// 环检测论证同 <see cref="AddDerived"/>：新属性尚未登记，不可能成环。</summary>
    public void AddStacked(AttributeSpec spec, string seedExpression)
    {
        ArgumentNullException.ThrowIfNull(spec);
        EnsureNameAvailable(spec.AttributeName);
        Guard.NotBlank(seedExpression, nameof(seedExpression));
        var compiled = new CompiledExpression(seedExpression);
        ValidateDependencies(spec.AttributeName, compiled);
        AddAttributeCore(spec, new StackedCalculation(compiled), initialValue: null);
    }

    /// <summary>Add 族的共同装配（变异时序的 Add 等价物）：调用方已完成守卫、编译与依赖存在性检查，
    /// 本方法依次写时复制 → 建 Attribute 并登记 → 布依赖边 → 绑定求值器 → 拓扑全量重排。
    /// 失效传播：新属性无缓存且尚无人依赖它，无需失效（design D9）。
    /// 环检测：新属性名不在任何既有计算的依赖里，依赖存在性检查又保证新边只指向既有名字，
    /// 因而不可能成环（design D8）。</summary>
    private void AddAttributeCore(AttributeSpec spec, IAttributeCalculation? calculation, AttributeValueType? initialValue)
    {
        EnsureMutableTables();

        var attributeName = spec.AttributeName;
        var attribute = calculation is null
            ? initialValue.HasValue
                ? Attribute.CreateBase(spec, initialValue.Value)
                : Attribute.CreateBase(spec)
            : Attribute.CreateDerived(spec);
        attribute.Owner = this;
        _attributes.Add(attributeName, attribute);

        if (calculation is null)
            return;

        _calculations.Add(attributeName, calculation);
        foreach (var dep in calculation.Dependencies)
        {
            if (!_directDependents.TryGetValue(dep, out var dependents))
                _directDependents[dep] = dependents = new List<string>();
            dependents.Add(attributeName);
        }
        BindCalculation(attributeName, calculation);
        RecalculateTopologicalOrder();
    }

    /// <summary>同名守卫：属性名已存在时抛出，既有属性不受影响。</summary>
    private void EnsureNameAvailable(string attributeName)
    {
        if (_attributes.ContainsKey(attributeName))
            throw new InvalidOperationException($"无法添加属性：'{attributeName}' 已存在。");
    }

    /// <summary>依赖存在性检查：表达式的每个依赖名必须已存在于本实例，
    /// 否则抛出（错误信息同时含表达式文本与未解析名）。action 描述变异动作，用于错误信息措辞。</summary>
    private void ValidateDependencies(string attributeName, CompiledExpression compiled, string action = "添加属性")
    {
        foreach (var dep in compiled.Dependencies)
        {
            if (!_attributes.ContainsKey(dep))
                throw new InvalidOperationException(
                    $"{action} '{attributeName}' 的表达式 '{compiled.ExpressionText}' " +
                    $"引用了不存在的属性：'{dep}'。");
        }
    }

    /// <summary>首次变异前私有化三张静态表（design D7）：深拷贝 _directDependents（字典 + 内层 List）
    /// 与 _topoIndex；_calculations 字典浅拷、其中 StackedCalculation 换入 Copy() 的私有副本——
    /// seed/贡献的 CompiledExpression 与编译委托不可变、继续共享，DerivedCalculation record 继续共享。
    /// 计算对象被换为私有副本后必须重绑求值器：BindCalculation 的闭包捕获计算对象引用，
    /// 不重绑则闭包仍捕获共享原件，后续对私有副本的变异（贡献增撤）对求值完全不可见；
    /// DerivedCalculation 为共享的同一对象，重绑无害幂等。此后再变异直接改私有表；
    /// 共享族中的其它实例（含未变异的克隆）不受影响。
    /// 每个变异入口先经本方法；守卫与编译先于本方法执行，失败时变异未生效、owner 状态不变。</summary>
    private void EnsureMutableTables()
    {
        if (!_tablesSharedWithClones)
            return;

        var directDependents = new Dictionary<string, List<string>>(_directDependents.Count, AttributeNames.Comparer);
        foreach (var (name, dependents) in _directDependents)
            directDependents[name] = new List<string>(dependents);

        var calculations = new Dictionary<string, IAttributeCalculation>(_calculations.Count, AttributeNames.Comparer);
        foreach (var (name, calculation) in _calculations)
            calculations[name] = calculation is StackedCalculation stacked ? stacked.Copy() : calculation;

        var topoIndex = new Dictionary<string, int>(_topoIndex, AttributeNames.Comparer);

        _directDependents = directDependents;
        _calculations = calculations;
        _topoIndex = topoIndex;
        _tablesSharedWithClones = false;
        BindCalculations();
    }

    /// <summary>全量重算拓扑序（属性规模为几十，全量重排成本可忽略，design D8）。</summary>
    private void RecalculateTopologicalOrder()
        => _topoIndex = AttributeTopology.BuildTopologicalOrder(_calculations);

    /// <summary>向聚合栈 derived 投一条贡献：expression 是与 seed 同构的完整 NCalc 表达式
    /// （独立解析、提取依赖、求值），其依赖变化经既有失效流程触发重算。
    /// 错误在变异调用时暴露——目标不存在、目标不是聚合栈 derived、表达式空白/语法错误/
    /// 未知引用、贡献成环均即时抛出且 owner 状态不变（守卫全部先于任何状态变更）。
    /// 成功后失效 target 自身及其下游（复用既有两阶段流程：标脏快照 → 按拓扑序分发，
    /// NewValue 惰性，事件不新增种类），返回撤销专用句柄（一次性，绑定本 owner 与目标名）。</summary>
    public ContributionHandle Contribute(string targetName, ContributionKind kind, string expression)
    {
        Guard.NotBlank(targetName, nameof(targetName));
        Guard.NotBlank(expression, nameof(expression));

        // 形态守卫：仅聚合栈 derived 接受贡献（base / plain derived 均拒绝，指明目标与原因）
        if (!_attributes.TryGetValue(targetName, out var target))
            throw new KeyNotFoundException($"AttributeOwner 中不存在名为 '{targetName}' 的属性。");
        if (!target.IsDerived)
            throw new InvalidOperationException(
                $"无法向 '{targetName}' 投贡献：base 属性不接受贡献，仅聚合栈 derived 属性接受。");
        if (_calculations[targetName] is not StackedCalculation)
            throw new InvalidOperationException(
                $"无法向 '{targetName}' 投贡献：plain derived 属性不接受贡献，仅聚合栈 derived 属性接受。");

        var compiled = new CompiledExpression(expression);
        ValidateDependencies(targetName, compiled, "为属性添加贡献");
        ValidateContributionAcyclic(targetName, compiled.Dependencies);

        EnsureMutableTables();
        var stacked = (StackedCalculation)_calculations[targetName]; // COW 后必为私有副本
        var handle = new ContributionHandle(this, targetName);

        var dependenciesBefore = stacked.Dependencies;
        stacked.Add(kind, compiled, handle);
        UpdateDependentEdges(targetName, dependenciesBefore, stacked.Dependencies);
        RecalculateTopologicalOrder();
        InvalidateDependents(target);

        return handle;
    }

    /// <summary>撤销一条贡献：句柄是一次性凭证，撤销后即失效（Revoked 置位）。
    /// null 句柄、跨 owner 句柄、重复撤销、目标属性已不存在均在本调用即时抛出，
    /// owner 状态不变。撤销成功后失效 target 自身及其下游（复用两阶段失效流程）。</summary>
    public void RemoveContribution(ContributionHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (!ReferenceEquals(handle.Owner, this))
            throw new InvalidOperationException("无法撤销贡献：句柄由其它 owner 签发，不可跨 owner 使用。");
        if (handle.Revoked)
            throw new InvalidOperationException("无法撤销贡献：句柄已撤销过，一次性凭证不可重复使用。");
        if (!_attributes.TryGetValue(handle.TargetName, out var target))
            throw new InvalidOperationException(
                $"无法撤销贡献：目标属性 '{handle.TargetName}' 已不存在。");

        EnsureMutableTables();
        var stacked = (StackedCalculation)_calculations[handle.TargetName];
        var dependenciesBefore = stacked.Dependencies;
        if (!stacked.Remove(handle))
            throw new InvalidOperationException(
                $"无法撤销贡献：句柄在 '{handle.TargetName}' 的聚合栈中不存在（状态不一致）。");
        handle.Revoked = true;

        UpdateDependentEdges(handle.TargetName, dependenciesBefore, stacked.Dependencies);
        RecalculateTopologicalOrder();
        InvalidateDependents(target);
    }

    /// <summary>按名移除属性：被任何 derived 表达式、聚合栈 seed 或贡献项引用时拒绝并指明引用方
    /// （引用统一以各计算条目的依赖并集呈现，无需区分引用来源，design D10）。
    /// 错误在本调用即时暴露——属性不存在、仍被引用均抛出且 owner 状态不变。
    /// 无需失效传播：引用检查保证无人依赖被移除属性，它没有下游。
    /// 被移除属性若是带在位贡献的聚合栈，允许移除——贡献随宿主一起消失（无外部引用），
    /// 其句柄此后撤销会命中既有"目标属性已不存在"守卫（见 RemoveContribution）。</summary>
    public void RemoveAttribute(string attributeName)
    {
        Guard.NotBlank(attributeName, nameof(attributeName));
        if (!_attributes.TryGetValue(attributeName, out var attribute))
            throw new KeyNotFoundException($"AttributeOwner 中不存在名为 '{attributeName}' 的属性。");

        foreach (var (name, calculation) in _calculations)
        {
            if (calculation.Dependencies.Contains(attributeName))
                throw new InvalidOperationException(
                    $"无法移除属性 '{attributeName}'：它被 '{name}' 的计算引用。");
        }

        EnsureMutableTables();

        // 出边清理：若被移除属性自身是 derived，撤销"它引用别人"的边——
        // 引用检查已保证没有"别人引用它"的边，这是本路径唯一需要维护的方向
        if (_calculations.Remove(attributeName, out var removedCalculation))
        {
            UpdateDependentEdges(attributeName, removedCalculation.Dependencies, Array.Empty<string>());
            RecalculateTopologicalOrder();
        }

        _attributes.Remove(attributeName);

        // 以该名为键的依赖边条目：引用检查后应已为空表，防御性清理
        _directDependents.Remove(attributeName);
    }

    /// <summary>贡献路径的环检测（design D8，唯一可能成环的路径）：贡献生效将引入
    /// target 依赖各贡献依赖名的新边，成环 ⟺ 某个新依赖名经既有依赖方向（_calculations 的
    /// Dependencies）可达 target。可达即抛循环依赖异常（错误信息含环路属性名链），贡献未生效。
    /// 既有依赖图无环（构建期与历次变异保证），故各起点的 DFS 不会误报既有边为环。</summary>
    private void ValidateContributionAcyclic(string targetName, IReadOnlyList<string> contributionDependencies)
    {
        var state = new Dictionary<string, int>(AttributeNames.Comparer); // 0/缺失=未访问 1=访问中 2=完成
        var path = new List<string>();

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
            foreach (var dep in _calculations[name].Dependencies)
            {
                if (AttributeNames.Comparer.Equals(dep, targetName))
                {
                    // 既有链 起点 -> ... -> name -> target 加上贡献新边 target -> ... -> 起点 闭合
                    var cycle = path.Append(targetName).Append(path[0]);
                    throw new InvalidOperationException(
                        "检测到循环依赖：" + string.Join(" -> ", cycle) + "。");
                }
                if (_calculations.ContainsKey(dep))
                    Visit(dep);
            }
            path.RemoveAt(path.Count - 1);
            state[name] = 2;
        }

        foreach (var dependencyName in contributionDependencies)
        {
            if (_calculations.ContainsKey(dependencyName))
                Visit(dependencyName);
        }
    }

    /// <summary>按 target 变异前后的依赖并集差分维护直接依赖边（并集语义：同一依赖名可能同时被
    /// seed 与多条贡献引用，target→dep 的直接依赖边至多一条，增删都必须以"并集是否包含该名"为准，
    /// 而非单条贡献的增撤——否则撤销一条贡献会撤掉仍被 seed 或其它贡献引用的边）。
    /// before/after 为 StackedCalculation 变异前后的 Dependencies（并集整体替换、旧列表不可变，
    /// 差分安全）。</summary>
    private void UpdateDependentEdges(
        string targetName, IReadOnlyList<string> dependenciesBefore, IReadOnlyList<string> dependenciesAfter)
    {
        var before = new HashSet<string>(dependenciesBefore, AttributeNames.Comparer);
        var after = new HashSet<string>(dependenciesAfter, AttributeNames.Comparer);

        foreach (var dep in dependenciesAfter)
        {
            if (before.Contains(dep))
                continue;
            if (!_directDependents.TryGetValue(dep, out var dependents))
                _directDependents[dep] = dependents = new List<string>();
            dependents.Add(targetName);
        }

        foreach (var dep in dependenciesBefore)
        {
            if (after.Contains(dep))
                continue;
            if (_directDependents.TryGetValue(dep, out var dependents))
                dependents.Remove(targetName);
        }
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

    /// <summary>为每个 derived 绑定求值器，builder 装配与克隆共用此路径；单条绑定见 <see cref="BindCalculation"/>。</summary>
    private void BindCalculations()
    {
        foreach (var (attributeName, calculation) in _calculations)
            BindCalculation(attributeName, calculation);
    }

    /// <summary>为单个 derived 绑定求值器：闭包捕获计算对象、调用时取"当前"依赖集合的值——
    /// plain 形态依赖不变，行为等价于绑定时捕获依赖列表；聚合栈形态的依赖并集随贡献增撤动态变化，
    /// 每次求值都取最新并集。构造装配与 Add 共用此路径，保证两条来源的绑定行为一致。</summary>
    private void BindCalculation(string attributeName, IAttributeCalculation calculation)
        => this[attributeName].BindEvaluator(
            () => calculation.Calculator(GetDependencyValues(calculation.Dependencies)));

    /// <summary>失效传播：以 source 为根沿依赖图收集受影响 derived——含根自身（贡献路径以聚合栈
    /// derived 为根、必须失效其自身；base 写值路径的根是 base、天然不在收集集），随后两阶段处理。
    /// 剪枝：出队去重后，已脏 derived（Attribute.IsDirty）直接跳过——不加入收集集（不重发通知、
    /// 不做空操作快照）、不展开其下游；根统一适用同一规则、不特判。
    /// 剪枝不变量：若 derived 为脏，则其全部传递下游亦为脏——失效波按完整闭包标脏、读取只洗净
    /// 被读节点的上游闭包，归纳可得。并集闭包保证：带剪枝的波"新标脏 ∪ 原有脏"仍等于完整闭包，
    /// 任何读取都拿不到过期缓存。合并语义：失效通知仅在缓存由有效转为失效时发出，已脏期间
    /// 后续变更不重复通知，读取即得含全部变更的新值。
    /// 演进约束：未来新增任何标脏/失效来源的变异路径，都必须维持"完整闭包标脏"——
    /// 只标脏部分下游会破坏不变量，使剪枝漏掉仍持过期缓存的下游。</summary>
    internal void InvalidateDependents(Attribute source)
    {
        if (_directDependents.Count == 0)
            return;

        List<Attribute>? affected = null;
        var visited = new HashSet<string>(AttributeNames.Comparer);
        var queue = new Queue<string>();
        queue.Enqueue(source.Name);

        while (queue.Count > 0)
        {
            var name = queue.Dequeue();
            if (!visited.Add(name))
                continue;
            var attribute = _attributes[name];
            if (attribute.IsDerived)
            {
                // 已脏：整支剪枝——不重发通知、不展开下游（不变量：脏的传递下游必已脏，波后并集闭包仍完整）
                if (attribute.IsDirty)
                    continue;
                (affected ??= new List<Attribute>()).Add(attribute);
            }
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
