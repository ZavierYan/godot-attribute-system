# Proposal

## Why

`AttributeChangedEventArgs` 的三个字段中两个是冗余的：`Name` 与事件的 sender（即 `Attribute` 本身）完全重复；`NewValue` 的惰性是假象——其 provider 就是 `ComputeValue`，与读取 `attribute.Value` 走同一条路（同样的缓存、同样的 memoization，连延迟访问语义都一致），不创造任何 `Value` getter 没有的语义。唯一不可省略的是 `OldValue`：处理器运行时旧值已无处可读，必须随事件传递。裁掉包装类让事件签名更诚实（"拿到旧值，新值请读属性本身"），同时删掉一份惰性契约的重复文档与防御性抛错的维护面。

## What Changes

- **BREAKING**：删除 `AttributeChangedEventArgs` 类
- **BREAKING**：`Attribute.ValueChanged` 事件类型从 `EventHandler<AttributeChangedEventArgs>?` 改为 `Action<Attribute, AttributeValueType?>?`——参数为发生变化的属性与旧值（`oldValue`）
- 新值不再由事件携带：订阅方需要新值时读取 `attribute.Value`；derived 的惰性求值语义不变（读取才重算、未读取不求值）
- 同步迁移测试中约 10 处 `e.NewValue` / `e.OldValue` / `e.Name` 用法，更新 README 观察节

## Capabilities

### New Capabilities

（无）

### Modified Capabilities

- `attribute-observation`：观察者协议的载荷契约措辞调整——「订阅 base 属性的值变化」的事件携带物从"属性名、旧值与新值"改为"发生变化的属性与旧值（新值经该属性读取）"；「derived 属性的失效通知」的惰性语义从"事件中的新值惰性暴露"改为"读取该属性即惰性求值"。通知时机、合并语义、拓扑顺序、订阅安全等 requirement 均不变。
- `attribute-owner`：「运行时贡献与撤销」中引用事件载荷的措辞（"含惰性新值"）与一个场景 THEN（"事件的新值为惰性求值"）改为指向经属性读取的惰性求值；贡献协议本身不变。

## Impact

- 代码：`addons/attribute-system/Attribute.cs`（删除 args 类、改事件签名与两处触发点）
- 测试：`addons/attribute-system/tests/ObservationTest.cs`、`addons/attribute-system/tests/RuntimeInjectionTest.cs`（机械迁移）
- 文档：`addons/attribute-system/README.md` 观察节两处示例
- 公共 API breaking：该 addon 的既有订阅方需改订阅签名；规格层为上述两个能力各产出 delta spec
