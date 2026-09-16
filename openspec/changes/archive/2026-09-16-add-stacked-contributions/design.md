## Context

现有架构三层切分：`AttributeSpec`（定义）/ `AttributeOwnerBuilder`（组装）/ `AttributeOwner`（实例）。owner 持有四张表：`_attributes`、`_directDependents`（失效图）、`_topoIndex`（失效分发的拓扑序）、`_calculations`（每 derived 一条 `DerivedCalculation(Dependencies, Calculator)` record）。三张静态表构建后约定不可变，且在克隆族之间**直接共享**（`Clone()` 传引用）；`DerivedCalculation` 不保留表达式文本，克隆族共享编译委托。表达式（NCalc）是依赖关系的唯一声明来源：`CompiledExpression` 解析一次、`IdentifierCollector` 提取依赖、`AsCalculator()` 编译为统一委托。`Attribute` 对求值通道无感知：derived 的求值器是盲委托 `Func<AttributeValueType>`，`ComputeValue()` 调委托、尾端钳制、写缓存。base 写值沿失效图按拓扑序两阶段失效（标脏快照 → 分发事件，事件新值惰性）。

约束：主线程使用约定（委托求值非线程安全，同族共享是设计强制）；错误尽早暴露哲学（构建期错误全部在 Build 抛、守卫参数即时抛）。

## Goals / Non-Goals

**Goals:**

- derived 支持聚合栈形态：seed + 三类类型化贡献，运行期可增撤，协议 `(seed + Σ flat) × (1 + Σ additive) × Π(1 + multiplicative)`，桶内交换律。
- owner 支持运行时增删属性与贡献，错误在变异调用时暴露。
- 既有行为零变化：plain derived、base 读写、克隆契约、事件、钳制。
- 克隆族隔离：变异不波及既有克隆，变异后的 owner 再克隆继承注入。

**Non-Goals:**

- 修正生命周期（持续时间、叠加规则、来源追踪、过期自动移除）——归 effect/装备系统。
- 顺序/优先级维度（分层管线）——分段语义由 derived 阶段链（derived 依赖 derived）表达，现有机制已覆盖。
- 原子化"模块清单 attach/detach"、属性集合变化事件、线程安全——等真实需求。
- 撤销贡献之外的批量操作 API。

## Decisions

### D1: 类型化三桶协议，桶内交换律，无顺序维度

贡献分 flat / additive / multiplicative 三桶，协议公式固定且在声明处可见。桶内加法/乘法满足交换律，系统不引入任何顺序、优先级概念。备选否决：分层管线 `old => f(old)`（表达力最高但顺序成为一等概念、模块耦合"前值"）；"百分比只乘 base"类分段语义用 derived 阶段链表达（`attackBaseBoosted = attackbase * 1.5` 作为普通 derived，聚合栈 seed 指向它），依赖图结构表达分段、贡献系统表达叠加，两个正交维度各用各的机制。

### D2: NCalc 项级复用 + C# 折叠组合，不拼串重编译

seed 与每条贡献各自是一个完整 NCalc 表达式（解析、依赖提取、求值、转换、报错信息全部复用 `CompiledExpression`）；三段组合是 C# 折叠代码：聚合求值器在依赖值全集上先求 seed、累加 flat、求和 additive、累乘 (1+m)，返回原始值（钳制仍由 `Attribute.ComputeValue()` 尾端统一执行，天然"只钳最终结果"）。备选否决：把 seed 与贡献拼成一个表达式字符串整体重编译——每次增撤全量重编译、必须保留 seed 文本、报错信息对不上贡献边界。委托组合是增量的：加一条贡献只编译那一行。

### D3: 计算层新增 `StackedCalculation`，`Attribute` 零改动

聚合栈 derived 的实例仍是 `Attribute.CreateDerived(spec)`——盲委托插槽绑上聚合求值器，缓存/钳制/事件/克隆全走既有路径。"是否聚合栈"不写进 Attribute 实例，由 `_calculations` 表中值的运行时类型决定（贡献守卫查表即知），状态只存一份。新增 internal 类型：

- `StackedCalculation`（class，可变）：seed `CompiledExpression` + 三桶 `List<Contribution>`，`Contribution = (ContributionKind, CompiledExpression, ContributionHandle)`；维护依赖并集（增撤时全量重算并集，贡献数小）；`Compute(dependencyValues)` 实现三段折叠。
- `ContributionKind`（public enum：Flat / Additive / Multiplicative）。
- `ContributionHandle`（public，绑定 owner、一次性）：记录所属 owner 与撤销状态；跨 owner 撤销、重复撤销抛异常。
- `DerivedCalculation` record 仅加接口实现声明（见 D4），语义零改动。

### D4: 计算表以共同接口统一，plain 与聚合栈同签名

`_calculations` 的值类型改为 internal 接口 `IAttributeCalculation { IReadOnlyList<string> Dependencies; AttributeCalculator Calculator; }`：`DerivedCalculation`（record，天然成员匹配）与 `StackedCalculation` 实现之。owner 的装配、求值、环检测、引用检查对两种形态无感知，仅贡献守卫处做一次类型判别。`StackedCalculation.Dependencies` 是动态并集，`BindCalculations` 的闭包从"绑定时捕获依赖列表"改为"捕获计算对象、调用时取当前 Dependencies"——plain 形态依赖不变、行为等价。

### D5: 公开 API 形态：owner 原语，非模块清单

- builder：`RequireStacked(attributeName, seedExpression)` 声明聚合栈成员；空白 seed 声明时即时抛；同名冲突网扩展——相同 seed 幂等、不同 seed / plain 与聚合栈混合归入既有 `_conflictedNames` 机制（Build 时报冲突的重复声明）。声明形态在声明表中记录（base / plain 表达式 / 聚合栈 seed），按形态与文本判幂等与冲突。
- owner：`AddBase(AttributeSpec, initialValue?)`、`AddDerived(AttributeSpec, expression)`、`AddStacked(AttributeSpec, seedExpression)`、`Contribute(targetName, kind, expression) → ContributionHandle`、`RemoveContribution(handle)`、`RemoveAttribute(name)`。Add 族收 `AttributeSpec`（owner 不持有 spec 库，spec 由调用方模块从库取得）。
- 备选否决："模块清单"原子 attach/detach（声明集先整体验证再整体生效）——YAGNI，effect 系统自行按序调用原语即可；原子性等真实需求出现再加。

### D6: 变异时序——错误在变异调用时暴露

每次变异按固定次序：守卫（存在性/形态/空白/句柄）→ 编译（语法错误即时抛）→ 依赖名存在性检查（错误信息含表达式文本与未解析名）→ 环检测 → 写时复制（D7）→ 布边/撤边 → 拓扑全量重排（D8）→ 失效目标及下游（两阶段：标脏快照 + 事件，复用既有流程）。任何一步失败则变异未生效、owner 状态不变。这是"构建期错误在 Build 抛"的运行时等价物：每个变异调用自身原子。

### D7: 写时复制拆克隆族共享

`AttributeOwner` 增加 `_tablesSharedWithClones` 标志：克隆路径产出的实例为 true，builder 装配路径为 false。首次变异前 `EnsureMutableTables()`：若为 true，深拷贝 `_directDependents`（字典 + 内层 List）、`_topoIndex`、`_calculations`（字典浅拷 + 其中 `StackedCalculation` 深拷贝——seed/贡献的 `CompiledExpression` 与委托不可变、继续共享；`DerivedCalculation` record 继续共享），然后置 false。此后再变异直接改。克隆时新实例标志重置为 true（与自己的克隆族共享）。效果：变异不波及既有克隆（反之亦然），变异后的 owner 再克隆，克隆继承注入的属性与贡献，base 继承彼时当前值——与既有克隆契约一致。

### D8: 环检测只存在于贡献路径；拓扑全量重排

`AddDerived` / `AddStacked` 不可能成环：新属性刚加入，不可能出现在任何既有表达式的依赖里。唯一环风险是 `Contribute`：贡献引入"目标依赖新贡献依赖名"的新边，成环 ⟺ 某个新贡献依赖名经既有依赖方向（`_calculations` 的 Dependencies）可达目标自身（新边与既有链闭合）。检测：从每个新贡献依赖名沿依赖方向 DFS，可达目标即抛循环依赖异常（错误信息含环路属性名）。拓扑序：每次变异后全量重算 `_topoIndex`（属性规模为几十，全量重排成本可忽略），失效分发的拓扑正确性由重排保证。

### D9: 失效触发点

`AddBase` / `AddDerived` / `AddStacked`：新属性无缓存、无人依赖它，无需失效。`Contribute` / `RemoveContribution`：失效目标自身及其下游（以目标为源走既有两阶段流程——标脏快照、按拓扑序分发、事件新值惰性）。`RemoveAttribute`：仅允许移除无引用者，无边可清、无下游受影响。

### D10: 移除属性的引用检查

`RemoveAttribute` 遍历 `_calculations` 各条目的 Dependencies（plain 表达式、seed、贡献的依赖统一以并集呈现），含该名即拒绝并指明引用方（引用者属性名）。检查依赖并集即可，无需区分引用来自哪类声明。

### D11: 事件与并发不新增

贡献增撤复用既有失效事件（`ValueChanged` 失效形态 + 惰性 NewValue），不新增事件种类；属性集合变化（添加/移除）不发事件。并发模型维持既有约定：仅主线程使用，克隆族共享编译委托（含贡献的 `CompiledExpression`）与现状一致。

## Risks / Trade-offs

- [克隆族共享表深拷贝遗漏内层 List 或 StackedCalculation，导致变异污染其它实例] → COW 逻辑集中在一个私有方法并单测锁定"变异不波及既有克隆 / 克隆上的变异不影响原型"；克隆继承注入单测锁定。
- [依赖并集在撤销贡献后未正确收缩，残留幽灵依赖边] → 撤销时全量重算并集（不增量删），并单测"撤销全部引用后可移除属性"兜底。
- [三段折叠的浮点中间溢出或 additive 池非正产生反直觉值] → 协议明示中间段不钳制、最终统一钳制；additive 池非正是合法输入（规格场景锁定钳制兜底），文档写明。
- [变异 API 扩大 owner 攻击面，误用（如对 plain derived 投贡献）] → 全部守卫在变异调用时抛异常，错误信息指明目标与原因；非法状态不可表达的目标不变。
- [owner 源码膨胀] → 变异原语可拆 internal 辅助（环检测、拓扑重排、COW）保持 `AttributeOwner.cs` 可读；具体拆分由实现裁量，不改变公开 API。

## Migration Plan

纯增量变更，无 breaking：全部既有公开 API 与语义保持（`DerivedCalculation` 加接口声明不影响外部可见行为）。迁移即升级——无数据格式、无持久化、无调用方迁移步骤。回滚 = revert 单个交付提交。
