# Design

## Context

现行观察协议：`Attribute.ValueChanged` 为 `event EventHandler<AttributeChangedEventArgs>?`，args 携带 `Name`、`OldValue`（可空）、`NewValue`（base 事件为直值，derived 失效事件为惰性 provider，首次访问触发 `ComputeValue` 并 memoize）。触发点两处：base 写值（`Value` setter）与 derived 失效分发（`RaiseInvalidated`，provider 即方法组 `ComputeValue`）。动机见 proposal.md——args 三个字段中仅 `OldValue` 不可省略。

## Goals / Non-Goals

**Goals:**
- 事件签名收敛为"属性 + 旧值"，删除 `AttributeChangedEventArgs`
- 观察行为的可观测语义零变化：通知时机、合并、拓扑序、订阅安全、惰性求值全部不变
- 测试与 README 同步迁移，全量通过

**Non-Goals:**
- 不引入变更来源判别（base 写值 vs derived 失效）——见 Risks 中的未来论据记录
- 不收紧 `OldValue` 为非空类型
- 不动失效传播、合并语义、克隆隔离等任何非载荷逻辑

## Decisions

### D1 新签名：`event Action<Attribute, AttributeValueType?>? ValueChanged`

参数为（发生变化的属性，旧值）。备选：

- **保留 EventHandler 但 args 瘦身到只剩 `OldValue`**（否决）：args 只剩一个字段，等于给旧值加一层壳，类本身与其文档、防御抛错（"该事件未携带新值"）成为纯维护面。
- **`Action<AttributeValueType?>` 不带属性参数**（否决）：per-instance 事件订阅处通常已持有引用，但丢失跨属性复用同一 handler 的能力；带 `Attribute` 参数的代价仅一个参数位，且保留了与原 sender 语义的对应。
- **维持现状**（否决）：`args.NewValue` 与 `attr.Value` 走同一条 `ComputeValue` 路径（同样的缓存与 memoization，provider 闭包读访问时刻状态，连延迟访问语义都一致），不创造任何 `Value` getter 没有的语义。

### D2 `OldValue` 保持可空

base 写值路径旧值恒非空；derived 失效路径按合并语义恒为缓存快照（coalesce 变更 D3），null 仅为 `TakeInvalidationSnapshot` 的防御分支。虽是 breaking 变更、理论上可收紧为非空，但防御分支的存在本身就是"不变量 I 可能被未来变异路径破坏"的保险——把保险丝一路焊死到事件签名上，收益（订阅方少一个 `?.`）小于成本（防御姿态断裂）。备选（收紧为 `AttributeValueType`）否决。

### D3 惰性契约收拢到 `Value`

惰性从来住在 `ComputeValue`（`Value` getter 的注释已声明"读取时惰性求值并缓存"），args 的 provider 只是间接层。删除后：`RaiseInvalidated` 直接传快照，args 的 `_newValueProvider` / `_newValue` memoization 字段与防御抛错一并消失；`Attribute` 与 `AttributeOwner` 的 XML 注释把"新值"表述统一指向"读取该属性"。

### D4 测试迁移形态

`e.Name` → 事件首参数的 `.Name`；`e.OldValue` → 第二参数；`e.NewValue` → 读取属性。惰性时序测试（通知发出时未访问不重算、访问才重算）改写为：handler 内先记录求值计数、再读 `attr.Value`，断言逐条同构——行为等价性由既有断言锁定。

## Risks / Trade-offs

- [订阅方在 handler 内读 `attr.Value` 触发波内重算] → 与原 `args.NewValue` 行为逐点等价（同一 `ComputeValue`、同一缓存），非新风险；测试锁定。
- [延迟读取拿到的是读取时刻的值而非通知发出时刻的值] → 原实现 provider 闭包读当前状态，行为本就如此，非行为变化；README 注明"新值经读取获得"。
- [订阅方无法区分 base 写值事件与 derived 失效事件] → 现状亦不可区分，本变更不使之恶化；若未来需要（如"仅 base 变化时刷新 UI"），届时引入判别载荷类型，本次不预付。
- [Godot 插件既有使用者订阅代码破坏] → 公共 API breaking，README 观察节给出迁移前后示例；项目历史一贯接受 breaking（连续 `feat!:` / `refactor!:`）。

## Migration Plan

单次源码变更（Attribute.cs + 测试 + README），构建产物即完成切换，无数据迁移。回滚即还原对应文件。
