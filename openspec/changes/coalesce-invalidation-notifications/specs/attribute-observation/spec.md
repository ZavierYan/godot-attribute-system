## MODIFIED Requirements

### Requirement: derived 属性的失效通知
当依赖的属性变化使 derived 属性缓存**由有效转为失效**时，该 derived 属性的订阅者 SHALL 收到变更通知；通知中新值 MUST 以惰性方式暴露——仅在订阅方访问时才触发重算，未访问则不产生求值。derived 已处于失效状态（重算前）期间发生的后续依赖变化 MUST NOT 触发重复通知——通知按脏状态迁移合并，订阅方需要数据时读取即得包含全部后续变化的新值。

#### Scenario: base 变化通知 derived 订阅者
- **WHEN** 订阅 derived 属性 `Attack`（表达式 `Strength * 2`，已被读取、缓存有效），随后 `Strength` 从 10 改为 15
- **THEN** `Attack` 的订阅者收到一次变更通知

#### Scenario: 新值惰性求值
- **WHEN** derived 属性失效通知已发出但订阅方尚未访问事件中的新值
- **THEN** 不发生求值计算；订阅方访问事件中的新值时才执行重算并得到 30

#### Scenario: 连续变更且中间未读取合并为一次通知
- **WHEN** `Attack`（表达式 `Strength * 2`）已被读取为 20，`Strength` 从 10 改为 15 使订阅方收到一次失效通知，订阅方未读取新值，随后 `Strength` 再次从 15 改为 20
- **THEN** `Attack` 的订阅方不再收到新的失效通知；此后读取 `Attack` 得到 40

#### Scenario: 失效后读取重算，再次变更重新通知
- **WHEN** `Attack` 失效通知后订阅方读取了新值 30（缓存重算洗净），随后 `Strength` 再次变化
- **THEN** 订阅方收到第二次失效通知，其旧值为上次读取的缓存值 30

#### Scenario: 从未读取过的 derived 首次读取前不通知
- **WHEN** `Attack` 自构建以来从未被读取（缓存为空），订阅方订阅后 `Strength` 发生变化
- **THEN** 订阅方不收到失效通知；首次读取 `Attack` 得到按当前依赖值计算的结果
