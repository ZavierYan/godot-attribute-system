## MODIFIED Requirements

### Requirement: 按属性名集合与表达式声明构建 Owner
`AttributeOwnerBuilder` SHALL 以 `Require(attributeName)` 声明 base 成员、`Require(attributeName, initialValue)` 声明携带初值的 base 成员、`Require(attributeName, expression)` 声明 derived 成员——成员资格与派生计算一次声明，表达式同时声明该属性的计算方法与依赖关系；derived 成员不提供初值声明（其值只能随表达式求值）。初值 SHALL 在构建时钳制到 spec 的 `[MinValue, MaxValue]` 区间，未声明初值的 base 成员从钳制后的默认值开始。同一属性名的重复 base 声明 SHALL 幂等：初值一致（含均未声明初值）时等价于声明一次，仅一方声明初值时合并取显式初值（与声明顺序无关）；两方声明不同初值、base/derived 混合声明、对同一属性名声明两条表达式 MUST 在构建调用时报错；表达式参数为空白在声明调用时即时抛出。构建产出恰好包含所声明属性名的 `AttributeOwner`，构建完成后 owner 内 SHALL 一律以 AttributeName 标识属性（访问、事件、表达式引用）。

#### Scenario: 构建成功
- **WHEN** 依次向 builder 声明 `Require(strength)`、`Require(dexterity)`、`Require(attack, strength+0.5*dexterity)`（属性定义均已在 spec 库登记）并调用构建
- **THEN** 得到一个可按属性名 `strength`、`dexterity`、`attack` 访问对应属性的 `AttributeOwner`，其中 `attack` 为 derived

#### Scenario: 同一属性名在不同构建中使用不同表达式
- **WHEN** 以同一属性名 `attack`（同一 spec）进行两次构建，一次声明 `Require(attack, strength+0.5*dexterity)`，另一次声明 `Require(attack, intelligence*1.2)`
- **THEN** 两次构建均成功，各自的 `attack` 为 derived 且按各自表达式求值

#### Scenario: 重复 base 声明幂等
- **WHEN** 同一属性名 `strength` 被声明两次 `Require(strength)` 后调用构建
- **THEN** 构建成功，owner 中恰好包含一个 `strength` 属性

#### Scenario: 声明初值的 base 成员从初值开始
- **WHEN** 声明 `Require(strength, 15)`（spec 的最值为 [0, 100]）并调用构建
- **THEN** 构建产物的 `strength` 当前值为 15

#### Scenario: 越界初值被钳制
- **WHEN** 某 spec 的 Min=0、Max=100，声明 `Require(strength, -5)` 并调用构建
- **THEN** 构建产物的 `strength` 当前值为 0

#### Scenario: 无初值与有初值声明合并取显式值
- **WHEN** 对同一属性名 `strength` 以任一顺序先后声明 `Require(strength)` 与 `Require(strength, 20)`，随后调用构建
- **THEN** 构建成功，owner 中恰好包含一个 `strength` 属性，其当前值为 20

#### Scenario: 同一属性名两个不同初值声明被拒绝
- **WHEN** 对同一属性名 `strength` 先后声明 `Require(strength, 15)` 与 `Require(strength, 20)`，随后调用构建
- **THEN** 构建抛出异常，错误信息指明 `strength` 存在冲突的重复声明

#### Scenario: base 与 derived 混合声明被拒绝
- **WHEN** 对同一属性名 `attack` 先后声明 `Require(attack)` 与 `Require(attack, strength*2)`，随后调用构建
- **THEN** 构建抛出异常，错误信息指明 `attack` 存在冲突的重复声明

#### Scenario: 同一属性名两条表达式声明被拒绝
- **WHEN** 对同一属性名 `attack` 先后声明 `Require(attack, strength*2)` 与 `Require(attack, strength*3)`，随后调用构建
- **THEN** 构建抛出异常，错误信息指明 `attack` 存在冲突的重复声明

#### Scenario: 声明空白表达式
- **WHEN** `Require(attack, ...)` 的表达式参数为空字符串或纯空白字符
- **THEN** 声明调用立即抛出异常，错误信息指明表达式非法

## REMOVED Requirements

### Requirement: prototype 克隆
**Reason**: 克隆语义反转为继承原型的 base 当前值，场景「克隆从初始值开始」须换名为继承语义；MODIFIED 按场景名逐一比对，携带场景换名会被校验拒绝。
**Migration**: 行为由 ADDED「prototype 克隆继承 base 当前值」整体接管，无兼容缺口。

## ADDED Requirements

### Requirement: prototype 克隆继承 base 当前值
任一构建完成的 owner 可作为原型，克隆产出与其构成相同的新 owner 实例。克隆实例与原型共享静态构成（属性 spec 引用、编译后的求值委托、依赖图与拓扑序）；运行时状态：base 属性 SHALL 继承原型的当前值（构建时声明的初值即原型的初始当前值），derived 属性缓存为空、首次读取时依克隆自身的 base 值惰性重算，事件订阅 MUST 不继承。原型与各克隆实例之间、克隆与克隆之间的属性值与失效传播 MUST 互不影响。克隆实例 SHALL 可继续作为原型再次克隆，继承其彼时的 base 当前值。

#### Scenario: 克隆得到同构成实例
- **WHEN** 克隆一个包含 `strength`（base）与 `attack`（表达式 `strength * 2`）的 owner
- **THEN** 克隆实例包含同名属性且最值一致，`attack` 按同一表达式求值

#### Scenario: 克隆继承原型的 base 当前值
- **WHEN** 原型的 `strength` 已被写为 42，随后克隆
- **THEN** 克隆实例的 `strength` 为 42

#### Scenario: 克隆的 derived 从继承的 base 值重算
- **WHEN** 原型的 `strength` 为 20、`attack` 表达式为 `strength * 2`，克隆后首次读取克隆实例的 `attack`
- **THEN** 得到 40（克隆的 `attack` 缓存为空，惰性重算）

#### Scenario: 克隆间值互不影响
- **WHEN** 存在同一原型的两个克隆，向其中一个克隆的 `strength` 写值
- **THEN** 另一个克隆与原型的 `strength` 不受影响

#### Scenario: 事件订阅不泄漏进克隆
- **WHEN** 原型的某属性已订阅值变化事件，随后克隆
- **THEN** 克隆实例的该属性无任何订阅；修改原型属性触发原型的订阅，修改克隆属性不触发

#### Scenario: 失效传播按实例隔离
- **WHEN** 修改原型或某个克隆实例的 `strength`
- **THEN** 仅该实例自身的 `attack` 缓存失效并重算，其它实例的 `attack` 缓存与值不受影响

#### Scenario: 克隆可再克隆
- **WHEN** 对一个已被写值的克隆实例再次克隆
- **THEN** 得到同样构成的新实例，其 base 属性继承该克隆彼时的当前值

### Requirement: 按名登记原型并从原型克隆
`AttributePrototypeLibrary` SHALL 以原型名为键登记原型：`Register(prototypeName, builder)` 在库内执行构建并以原型名索引保存构建产物，原型实例 MUST 不向登记方暴露（库外仅能经克隆获得新实例）。原型名库内唯一，同名登记 MUST 被拒绝；空白原型名在登记调用时即时抛出。builder 的一切构建期错误（缺失 spec、同名冲突声明、表达式语法错误、表达式未知引用、循环依赖）SHALL 在登记时暴露。`Clone(prototypeName)` SHALL 产出该原型的克隆；未登记的原型名 MUST 抛出异常，错误信息包含该原型名。库 SHALL 另提供尝试克隆、存在判断与已登记原型名的枚举。

#### Scenario: 登记后按名克隆
- **WHEN** 以声明 `Require(strength, 15)` 与 `Require(attack, strength * 2)` 的 builder 登记原型 `warrior`，随后按 `warrior` 克隆
- **THEN** 得到新实例：`strength` 为 15（登记声明的初值随克隆带出），`attack` 按同一表达式求值

#### Scenario: 同名登记被拒绝
- **WHEN** 向已存在原型名 `warrior` 的库再次登记 `warrior`
- **THEN** 登记调用抛出异常，错误信息指明 `warrior` 已存在

#### Scenario: 登记空白原型名
- **WHEN** 以空字符串或纯空白字符作为原型名登记
- **THEN** 登记调用立即抛出异常

#### Scenario: 克隆未登记的原型名
- **WHEN** 按未登记的原型名 `rogue` 克隆
- **THEN** 抛出异常，错误信息包含 `rogue`

#### Scenario: 尝试克隆与存在判断
- **WHEN** 对已登记的 `warrior` 与未登记的 `rogue` 分别尝试克隆并判断存在
- **THEN** `warrior` 的尝试克隆成功产出实例且存在判断为真；`rogue` 的尝试克隆返回失败（不抛异常）且存在判断为假

#### Scenario: 构建错误在登记时暴露
- **WHEN** 登记的 builder 声明了 spec 库中不存在的属性名
- **THEN** 登记调用抛出异常，而非留到首次克隆时

#### Scenario: 枚举已登记的原型名
- **WHEN** 库中登记了 `warrior` 与 `mage` 两个原型
- **THEN** 原型名枚举恰包含 `warrior` 与 `mage`
