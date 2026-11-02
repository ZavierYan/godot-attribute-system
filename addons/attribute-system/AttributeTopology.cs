namespace AttributeSystem;

/// <summary>计算表拓扑序的统一构建：builder 构建期与 owner 运行期变异共用同一算法，避免双份漂移。</summary>
internal static class AttributeTopology
{
    /// <summary>对计算表做 DFS 拓扑排序 + 环检测（报出环路属性链），返回 属性名 → 拓扑序号。
    /// 仅遍历 calculations 中的名字；依赖指向非 derived 名时自然跳过（base 名是入度为零的叶子）。</summary>
    internal static Dictionary<string, int> BuildTopologicalOrder(
        IReadOnlyDictionary<string, IAttributeCalculation> calculations)
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
            foreach (var dep in calculations[name].Dependencies.Where(calculations.ContainsKey))
                Visit(dep);
            path.RemoveAt(path.Count - 1);
            state[name] = 2;
            order.Add(name);
        }
    }
}
