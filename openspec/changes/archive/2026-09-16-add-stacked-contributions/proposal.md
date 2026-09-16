## Why

owner 的拓扑在构建时一次固化（闭世界）：外部系统（effect、装备）无法在运行时向 owner 注入新属性，也无法给已存在的 derived 属性追加依赖。"attack = attackbase + effectattackbase + equipmentattackbase" 这类随系统挂载/卸载而生长的数值，今天只能预声明全部可能的名字——模块无法引入新的属性名，卸载也无从回退。

## What Changes

- derived 属性新增第二种形态**聚合栈**（stacked）：声明期给定 seed 表达式，运行期以三类**类型化贡献**（flat / additive / multiplicative）增撤参与项，值 = `clamp((seed + Σ flat) × (1 + Σ additive) × Π(1 + multiplicative))`；additive 池求和（同稀释、可负抵消），multiplicative 独立复利；桶内满足交换律，系统不引入顺序/优先级维度。既有表达式形态（plain）derived 行为不变。
- owner 新增运行时注入原语：`AddBase`（含初值）、`AddDerived`（plain 表达式）、`AddStacked`（聚合栈）、`Contribute`（向聚合栈投贡献并返回句柄）、`RemoveContribution`（按句柄撤贡献）、`RemoveAttribute`（被引用时拒绝删除）。
- 增/撤贡献与增删属性沿用现有失效传播、懒求值、缓存与事件机制，不新增事件；最终结果一次钳制，中间段不钳。
- 克隆语义扩展：首次变异前经写时复制（copy-on-write）与克隆族拆分共享表；变异后的 owner 再克隆，克隆继承注入的属性与贡献（与既有克隆契约一致：base 继承当前值、derived 缓存空、事件订阅不继承）。
- 所有注入/撤除错误（表达式语法错误、未知依赖名、构成循环、目标不是聚合栈、同名重复、删除被引用属性）在变异调用时即时抛出——是"构建期错误在 Build 抛"哲学的运行时等价物。
- builder 新增聚合栈声明入口；同名冲突声明校验网扩展覆盖聚合栈 seed 声明。
- 修正的生命周期（持续时间、叠加规则、来源追踪）不进属性系统——由 effect/装备系统自行管理，属性系统只提供组合、失效与缓存。

无 breaking 变更：全部现有公开 API 与语义保持。

## Capabilities

### New Capabilities

（无——本 change 是两个既有能力域的扩展。）

### Modified Capabilities

- `derived-attributes`: 新增「聚合栈 derived 与类型化贡献」requirement（三段协议公式、桶内交换律、贡献项为完整表达式、撤销贡献的回退语义、additive 池非正时的钳制兜底）；修改「表达式同时声明计算方法与依赖关系」（聚合栈形态下，derived 的依赖集合为 seed 与全部贡献项表达式标识符的并集——"依赖只来自表达式标识符"的不变量保持并措辞升级）。
- `attribute-owner`: 新增「运行时注入属性与贡献」requirement（Add 族 / Contribute / Remove 族原语、错误在变异调用时暴露、与既有失效/事件/钳制机制的衔接）；修改「prototype 克隆继承 base 当前值」（运行时注入随克隆继承、变异与克隆族之间写隔离）。

## Impact

- 源码：`AttributeOwner.cs` 承担主要改动（变异原语、写时复制、环检测、拓扑重排）；`AttributeOwnerBuilder.cs` 新增聚合栈声明与冲突网扩展；新文件 `StackedCalculation`（seed + 三桶 + 依赖并集 + 三段折叠）与贡献类型枚举、贡献句柄。`Attribute.cs`、`CompiledExpression.cs`、`DerivedCalculation.cs` 零改动。
- 公开 API：`AttributeOwnerBuilder.RequireStacked`；`AttributeOwner.AddBase / AddDerived / AddStacked / Contribute / RemoveContribution / RemoveAttribute`；新类型 `ContributionKind`、`ContributionHandle`。
- 依赖：无新增第三方依赖，NCalc 继续作为项级求值器（seed 与每条贡献各一个表达式），三段组合为 C# 折叠代码。
- 测试与文档：测试基线 80 预计增至约 110；README 新增聚合栈与运行时注入章节；两个主规格 Purpose 同步直改。
