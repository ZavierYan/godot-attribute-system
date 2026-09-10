# attribute-owner Specification

## Purpose

定义 `AttributeOwner` 的构建协议：`AttributeOwnerBuilder` 依据所需属性名集合从 spec 库取定义，以成员声明携带的表达式声明派生计算，校验（缺失 spec、同名冲突声明、表达式未知引用、循环依赖）并构建出运行时按 AttributeName 访问、可读写 base 值并受最值钳制的属性容器；构建完成的实例可作为原型克隆出同构成的新实例。

## Requirements

### Requirement: 按属性名集合与表达式声明构建 Owner
`AttributeOwnerBuilder` SHALL 以 `Require(attributeName)` 声明 base 成员、`Require(attributeName, expression)` 声明 derived 成员——成员资格与派生计算一次声明，表达式同时声明该属性的计算方法与依赖关系。同一属性名的重复 base 声明 SHALL 幂等（等价于声明一次）；base/derived 混合声明、对同一属性名声明两条表达式 MUST 在构建调用时报错；表达式参数为空白在声明调用时即时抛出。构建产出恰好包含所声明属性名的 `AttributeOwner`，构建完成后 owner 内 SHALL 一律以 AttributeName 标识属性（访问、事件、表达式引用）。

#### Scenario: 构建成功
- **WHEN** 依次向 builder 声明 `Require(strength)`、`Require(dexterity)`、`Require(attack, strength+0.5*dexterity)`（属性定义均已在 spec 库登记）并调用构建
- **THEN** 得到一个可按属性名 `strength`、`dexterity`、`attack` 访问对应属性的 `AttributeOwner`，其中 `attack` 为 derived

#### Scenario: 同一属性名在不同构建中使用不同表达式
- **WHEN** 以同一属性名 `attack`（同一 spec）进行两次构建，一次声明 `Require(attack, strength+0.5*dexterity)`，另一次声明 `Require(attack, intelligence*1.2)`
- **THEN** 两次构建均成功，各自的 `attack` 为 derived 且按各自表达式求值

#### Scenario: 重复 base 声明幂等
- **WHEN** 同一属性名 `strength` 被声明两次 `Require(strength)` 后调用构建
- **THEN** 构建成功，owner 中恰好包含一个 `strength` 属性

#### Scenario: base 与 derived 混合声明被拒绝
- **WHEN** 对同一属性名 `attack` 先后声明 `Require(attack)` 与 `Require(attack, strength*2)`，随后调用构建
- **THEN** 构建抛出异常，错误信息指明 `attack` 存在冲突的重复声明

#### Scenario: 同一属性名两条表达式声明被拒绝
- **WHEN** 对同一属性名 `attack` 先后声明 `Require(attack, strength*2)` 与 `Require(attack, strength*3)`，随后调用构建
- **THEN** 构建抛出异常，错误信息指明 `attack` 存在冲突的重复声明

#### Scenario: 声明空白表达式
- **WHEN** `Require(attack, ...)` 的表达式参数为空字符串或纯空白字符
- **THEN** 声明调用立即抛出异常，错误信息指明表达式非法

### Requirement: 构建期校验缺失的 spec
当传入的属性名未在 spec 库登记时，构建 MUST 失败并抛出异常，错误信息指明缺失的属性名。

#### Scenario: 请求未登记的 spec
- **WHEN** builder 收到属性名 `hero-mana`，但 spec 库中没有该 AttributeName 的 spec
- **THEN** 构建抛出异常，错误信息包含 `hero-mana`

### Requirement: 构建期校验表达式的未知引用
注入的表达式中引用的属性名（AttributeName）不在本次构建得到的属性名集合内时，构建 MUST 失败并抛出异常，错误信息同时指明所在表达式与未解析的属性名。

#### Scenario: 表达式引用未包含的属性
- **WHEN** 对属性名 `attack` 注入表达式 `strength+0.5*dexterity`，但本次构建的属性名集合中不含 `dexterity`
- **THEN** 构建抛出异常，错误信息同时包含 `strength+0.5*dexterity` 与 `dexterity`

### Requirement: 构建期检测循环依赖
当 derived 属性（直接或间接）依赖自身时，构建 MUST 失败并抛出异常，错误信息指明参与循环的属性链。

#### Scenario: 两个属性互相依赖
- **WHEN** 构建属性 `a`（表达式 `b + 1`）与属性 `b`（表达式 `a + 1`）
- **THEN** 构建抛出循环依赖异常，错误信息包含环路上的属性名

### Requirement: Owner 按 AttributeName 访问属性
`AttributeOwner` SHALL 支持按属性名（AttributeName）获取属性；名称不存在时 MUST 抛出异常而非返回静默的默认值。

#### Scenario: 访问已包含的属性
- **WHEN** 在包含 `strength` 的 owner 上按 `strength` 获取属性
- **THEN** 返回对应的属性对象，可读取其当前值

#### Scenario: 访问未包含的属性
- **WHEN** 在不包含 `mana` 的 owner 上按 `mana` 获取属性
- **THEN** 抛出异常，错误信息包含 `mana`

### Requirement: base 属性可读写并受最值钳制
base 属性的值 SHALL 可直接设置与读取；设置时值 MUST 被钳制到 spec 的 `[MinValue, MaxValue]` 区间，读取返回钳制后的值。

#### Scenario: 设置越界值被钳制
- **WHEN** 某 spec 的 Min=0、Max=100，向该 base 属性设置 -5
- **THEN** 后续读取得到 0

#### Scenario: 设置区间内的值保持原样
- **WHEN** 某 spec 的 Min=0、Max=100，向该 base 属性设置 42
- **THEN** 后续读取得到 42

### Requirement: derived 属性不可直接写入
对 derived 属性的写入尝试 MUST 抛出异常；derived 的值只能通过其依赖的属性变化而改变。

#### Scenario: 写入 derived 被拒绝
- **WHEN** 对表达式为 `strength * 2` 的 derived 属性 `attack` 调用设值
- **THEN** 抛出异常，错误信息说明该属性为 derived 不可写

### Requirement: prototype 克隆
`AttributeOwner` SHALL 支持克隆：任一构建完成的 owner 可作为原型，克隆产出与其构成相同的新 owner 实例。克隆实例与原型共享静态构成（属性 spec 引用、编译后的求值委托、依赖图与拓扑序），但运行时状态全新——每个属性从初始值开始（不继承原型当前值）、缓存为空、事件无订阅。原型与各克隆实例之间、克隆与克隆之间的属性值与失效传播 MUST 互不影响。克隆实例 SHALL 可继续作为原型再次克隆。

#### Scenario: 克隆得到同构成实例
- **WHEN** 克隆一个包含 `strength`（base）与 `attack`（表达式 `strength * 2`）的 owner
- **THEN** 克隆实例包含同名属性且最值一致，`attack` 按同一表达式求值

#### Scenario: 克隆从初始值开始
- **WHEN** 原型的 `strength` 已被写为 42，随后克隆
- **THEN** 克隆实例的 `strength` 为初始值（钳制后的默认值），不为 42

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
- **WHEN** 对一个克隆实例再次克隆
- **THEN** 得到同样构成与初始状态的新实例
