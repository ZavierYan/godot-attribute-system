## Context

现行失效传播（`AttributeOwner.InvalidateDependents`）：base 写值 / 贡献增撤后从源点 BFS 收集完整下游闭包，两阶段处理——按拓扑序 `TakeInvalidationSnapshot` 清缓存（标脏），再按拓扑序分发失效事件（`NewValue` 惰性）。derived 的"脏"由 `Attribute._hasCachedValue == false` 表示：出生（构建/克隆/Add）即脏，读取（`ComputeValue`）洗净并缓存，失效波重新标脏。动机见 proposal.md —— 已脏属性上的重复通知与重复遍历不携带新信息。

## Goals / Non-Goals

**Goals:**
- 失效 BFS 在已脏 derived 处剪枝：不重复通知、不重走其下游分支。
- 保证剪枝后闭包失效完整性不变：任何读取路径拿不到过期缓存。
- 通知语义钉死为"脏状态迁移（缓存由有效转为失效）"，进规格。

**Non-Goals:**
- 不做批量写 API（一次写多个 base 合并走一波）——对症批量写场景的另一个独立提案，本次不做。
- 不改公共 API（事件类型、委托签名、EventArgs 字段一概不动）。
- 不改 base 属性事件（写值即发、与失效 BFS 无关）。

## Decisions

### D1 剪枝不变量：脏 ⟹ 全部传递下游已脏

剪枝的正确性依赖不变量 **I**：若 derived X 为脏，则所有（直接或间接）依赖 X 的 derived 也为脏。

归纳论证（对系统全部状态迁移）：
- **变脏只有三个来源**：①构建/克隆——全部 derived 缓存空（全体脏，I 平凡成立）；②失效波——BFS 收集完整闭包统一标脏；③运行时 Add（`AddDerived`/`AddStacked`）——新节点出生即脏且尚无人依赖它（无下游）。
- **洗净只有一个路径**：读取触发 `ComputeValue`，递归读取依赖——只洗净被读节点的**上游**闭包，从不触碰下游。
- 反证 I 不成立的最早时刻：存在 X 脏而下游 Y 干净。Y 干净 ⟹ Y 最后一次求值发生且其依赖链上的 X 被连带洗净 ⟹ X 此后变脏必来自一次失效波，而完整闭包的波必达 Y ⟹ Y 脏，矛盾。故 I 在现行实现下始终成立。
- **剪枝保持 I**（自归纳）：带剪枝的波只跳过已脏节点及其子树，"新标脏集合 ∪ 原有脏集合"仍等于完整闭包；波后 I 依旧成立。

替代方案（否决）：按值去重——比较新旧缓存值相同则不通知。要求波内先重算再比较，破坏"NewValue 惰性、未访问不求值"的既有规格承诺。

### D2 剪枝点与统一根处理

BFS 出队、`visited` 去重之后，在 `IsDerived` 分支内查询 `Attribute.IsDirty`（新增 internal 属性，`IsDerived && !_hasCachedValue`）：已脏 → `continue`——不加入 `affected`（不重通知，`TakeInvalidationSnapshot` 对已脏节点本就是返回 null 的空操作）、不展开下游（不重走分支）。

根统一适用同一规则，不特判：base 写值路径的根是 base（base 无脏概念）永不剪；`Contribute`/`RemoveContribution` 的根是聚合栈 derived，若目标已脏则整波自然无通知——与"已脏期间一切变更（含贡献增撤）并入同一次待重算"的合并语义一致，读取时拿到含贡献变化的新值。

替代方案（否决）：仅剪枝遍历但仍重发通知——通知分发正是重复成本的大头（每订阅方一次调用），且"已脏还通知"在合并语义下自相矛盾。

### D3 推论：失效通知的 OldValue 永非 null

通知仅在干净→脏迁移时发出，被通知者必有缓存值，故 `OldValue` 恒为失效前的缓存快照；"derived 从未计算过则为 null"的取值路径不再可达（从未计算 ⟹ 一直脏 ⟹ 永远被剪枝）。`TakeInvalidationSnapshot` 的 null 分支保留为防御；`AttributeChangedEventArgs.OldValue` 的可空类型签名不动（公共 API，非 breaking），仅更新文档措辞。这是合并语义的附带收益：每次通知的旧值都有意义。

### D4 规格 delta 边界：只动 attribute-observation 一个 requirement

- **attribute-observation**「derived 属性的失效通知」：正文钉死迁移语义；既有场景「base 变化通知 derived 订阅者」补前置条件（已被读取、缓存有效）——否则按字面在新语义下可翻车（从未读取时不通知）；新增三个合并语义场景。
- **derived-attributes**「失效沿依赖图传播」：后置条件式措辞（"所有下游缓存 MUST 被标记为失效"）——已脏属性在后续变更后依然处于失效状态，并集语义下后置条件保持成立；其全部场景均含中间读取。零 delta。
- **attribute-owner**「运行时贡献与撤销」：引用式措辞（"复用既有失效传播与事件"）——"既有"随本变更演化，不点名通知次数。零 delta。
- base 事件、拓扑序分发、处理器内订阅安全、克隆隔离等 requirement 的措辞与 API 无关或场景含中间读取，均不受影响。

### D5 兼容性

- 公共 API 零变化；新增 internal `Attribute.IsDirty`。
- 既有 114 例测试逐一核对：派生通知类测试波前均有中间读取（干净 → 正常通知，行为不变）；base 事件类测试不经过失效 BFS；多变异不断言通知数的测试只验最终值（并集闭包保证不变）。唯一例外 `ObservationTest.DerivedOldValueIsNullWhenNeverComputed` 锁定的正是本次否决的旧语义（未读取也通知、旧值 null），改写为合并语义；另新增 2 例锁定（114 → 116）。

## Risks / Trade-offs

- [订阅方以事件次数做变更日志/统计，感知次数与写入次数解耦] → 语义决策已确认（通知的职责是"告知脏"而非"逐次变更回放"）；NewValue 惰性下中间值本就从未物化，无信息损失；README 观察节明示合并语义。
- [波内重入（处理器中再写 base / 增撤贡献）时，嵌套波在外波已标脏区域不再发事件] → 外波阶段二的分发列表已预收集、不受影响；嵌套变更并入同一批待重算，读取必得含嵌套变更的新值（D1 闭包保证）。
- [不变量 I 依赖"完整闭包标脏"的隐性约定，未来新增变异路径若只标脏部分下游会破坏剪枝正确性] → `InvalidateDependents` 方法注释显式写明不变量与剪枝依据，作为后续变异路径的演进约束。

## Migration Plan

单次源码变更，无数据/接口迁移；构建产物即完成切换。回滚即还原两个源文件与测试。
