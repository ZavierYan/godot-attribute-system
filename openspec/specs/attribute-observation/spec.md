# attribute-observation Specification

## Purpose

定义属性系统的观察者协议：外部可订阅任一属性的值变化，变更事件携带发生变化的属性与旧值，新值经由该属性读取获得（derived 惰性求值）、通知按脏状态迁移合并（已失效未重算期间不重复通知），且事件分发过程中的订阅操作是安全的。

## Requirements

### Requirement: 订阅 base 属性的值变化
外部 SHALL 能订阅任意 base 属性的值变化事件；事件 SHALL 携带发生变化的属性与旧值，新值经由该属性读取获得。

#### Scenario: base 值变化触发事件
- **WHEN** 订阅 `Strength` 的值变化后将其从 10 改为 20
- **THEN** 订阅方收到一次事件，事件携带的属性为 `Strength`、旧值为 10，读取该属性得到 20

#### Scenario: 设置相同值不触发事件
- **WHEN** `Strength` 当前值为 10，再次设置为 10（钳制后仍为 10）
- **THEN** 不触发值变化事件

#### Scenario: 退订后不再收到事件
- **WHEN** 订阅 `strength` 变化并收到一次事件后退订，随后再次修改 `strength`
- **THEN** 该订阅方不再收到事件

### Requirement: 变更事件先于下游失效通知
当 base 属性值发生变化时，该属性自身的变更事件 SHALL 先于所有受影响 derived 属性的失效通知分发；即下游失效通知开始分发时，变更属性的订阅方已经收到过本次变更事件。

#### Scenario: 通知顺序以变更属性为起点
- **WHEN** 存在 `attack = strength * 2` 与 `total = attack + 5`，同一订阅方同时订阅 `strength`、`attack` 与 `total`，随后修改 `strength`
- **THEN** 收到通知的顺序为 `strength`、`attack`、`total`

### Requirement: derived 属性的失效通知
当依赖的属性变化使 derived 属性缓存**由有效转为失效**时，该 derived 属性的订阅者 SHALL 收到变更通知；通知携带发生变化的属性与旧值，该属性当前的值 MUST 以惰性方式求值——仅在订阅方读取时才触发重算，未读取则不产生求值。derived 已处于失效状态（重算前）期间发生的后续依赖变化 MUST NOT 触发重复通知——通知按脏状态迁移合并，订阅方需要数据时读取即得包含全部后续变化的新值。

#### Scenario: base 变化通知 derived 订阅者
- **WHEN** 订阅 derived 属性 `Attack`（表达式 `Strength * 2`，已被读取、缓存有效），随后 `Strength` 从 10 改为 15
- **THEN** `Attack` 的订阅者收到一次变更通知

#### Scenario: 新值惰性求值
- **WHEN** derived 属性失效通知已发出但订阅方尚未读取该属性
- **THEN** 不发生求值计算；订阅方读取该属性时才执行重算并得到 30

#### Scenario: 连续变更且中间未读取合并为一次通知
- **WHEN** `Attack`（表达式 `Strength * 2`）已被读取为 20，`Strength` 从 10 改为 15 使订阅方收到一次失效通知，订阅方未读取该属性，随后 `Strength` 再次从 15 改为 20
- **THEN** `Attack` 的订阅方不再收到新的失效通知；此后读取 `Attack` 得到 40

#### Scenario: 失效后读取重算，再次变更重新通知
- **WHEN** `Attack` 失效通知后订阅方读取该属性得到 30（缓存重算洗净），随后 `Strength` 再次变化
- **THEN** 订阅方收到第二次失效通知，其旧值为上次读取的缓存值 30

#### Scenario: 从未读取过的 derived 首次读取前不通知
- **WHEN** `Attack` 自构建以来从未被读取（缓存为空），订阅方订阅后 `Strength` 发生变化
- **THEN** 订阅方不收到失效通知；首次读取 `Attack` 得到按当前依赖值计算的结果

### Requirement: 级联失效的通知顺序
当一次变化沿依赖链传播时，通知 SHALL 按拓扑顺序分发：被依赖者先于依赖它的 derived 属性收到通知。

#### Scenario: 依赖链上的通知顺序
- **WHEN** 存在 `Attack = Strength * 2` 与 `Total = Attack + 5`，同一订阅方同时订阅 `Attack` 与 `Total`，随后修改 `Strength`
- **THEN** `Attack` 的通知先于 `Total` 的通知到达

### Requirement: 事件分发中的订阅操作安全
在事件分发过程中进行订阅或退订 MUST 不引发错误，且不影响当次分发的完整性（当次已开始的分发按快照继续）。

#### Scenario: 处理器内退订自身
- **WHEN** 某订阅方的处理器在收到事件的第一个动作是退订自己，且同属性还有另一个订阅方
- **THEN** 退订不引发异常，当次分发中另一个订阅方仍收到该事件，下次变更时退订方不再收到

#### Scenario: 处理器内订阅新订阅方
- **WHEN** 某处理器在收到事件时向同一属性订阅一个新的订阅方
- **THEN** 订阅不引发异常，新订阅方不因当次事件被调用（避免无限放大），下次变更时正常收到
