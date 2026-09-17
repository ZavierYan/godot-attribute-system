## Why

失效通知的职责是告知订阅方"数据已脏、需要时读取即得新值"。当前实现每次 base 写值 / 贡献增撤都对完整下游闭包重新 BFS，并向每个受影响 derived 重发失效通知——即使该 derived 在上次失效后从未被读取（一直处于脏状态）。对已脏属性的重复通知不携带新信息（订阅方已知数据是脏的，读取时必然拿到含全部后续变更的新值），重复遍历与重复分发是纯开销。

## What Changes

- `AttributeOwner.InvalidateDependents` 的 BFS 在**已脏**的 derived 处剪枝：不再向其重复发出失效通知，也不再重走其下游分支（不变量"脏 ⟹ 全部传递下游已脏"保证闭包失效完整性不受影响，详见 design）。
- 失效通知的触发粒度由"每次导致失效的变更"改为"**脏状态迁移（缓存由有效转为失效）**"：连续多次变更在下次读取前至多产生一次通知；读取重算（洗净）后再次变更会重新通知。
- 推论：失效通知仅在属性有缓存时发出，事件 `OldValue` 从此永远是非空的失效前缓存值（"derived 从未计算过则为 null"的取值路径不再可达）；`AttributeChangedEventArgs` 文档措辞随之更新，公共类型签名不变（非 breaking）。
- `Attribute` 新增 internal 属性 `IsDirty`（脏状态查询），公共 API 零变化。

## Capabilities

### New Capabilities

（无）

### Modified Capabilities

- `attribute-observation`: requirement「derived 属性的失效通知」——正文由"依赖变化导致缓存失效时通知"钉死为"缓存**由有效转为失效**时通知；已失效（重算前）期间的后续变更 MUST NOT 重复通知。既有场景「base 变化通知 derived 订阅者」补前置条件（目标已被读取、缓存有效）；新增三个场景：连续变更且中间未读取合并为一次通知、失效后读取重算再次变更重新通知（旧值为上次缓存值）、从未读取过的 derived 首次读取前不通知。

## Impact

- `addons/attribute-system/Attribute.cs`：新增 `internal bool IsDirty`；`AttributeChangedEventArgs` 与类文档注释中 OldValue 语义措辞更新。
- `addons/attribute-system/AttributeOwner.cs`：`InvalidateDependents` BFS 剪枝与注释重写。
- 测试：改写 `ObservationTest.DerivedOldValueIsNullWhenNeverComputed`（其锁定的是本次否决的旧语义），新增 2 例合并语义锁定（114 → 116）。
- `addons/attribute-system/README.md`：观察节订阅示例注释与要点补合并语义两句。
- 不影响：derived-attributes（「失效沿依赖图传播」为后置条件式措辞，并集语义下"全部下游缓存失效"保持成立）、attribute-owner（贡献增撤"复用既有失效传播与事件"，随既有语义演化）、attribute-definition、公共 API、克隆契约、聚合栈协议。
