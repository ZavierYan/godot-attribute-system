## MODIFIED Requirements

### Requirement: 按属性名集合与表达式注入构建 Owner
`AttributeOwnerBuilder` SHALL 接受一组 AttributeName，从 spec 库逐个取得对应 `AttributeSpec`，为每个属性名创建属性；对经 `WithExpression(attributeName, expression)` 注入了表达式的属性名，SHALL 以该表达式作为其计算方法与依赖声明（即 derived 属性），未注入的即 base 属性。注入目标 MUST 属于本次构建的属性名集合，同一属性名 MUST NOT 被重复注入，两项检查与其它构建期校验一并在构建调用时完成；注入参数自身的空白格式错误在注入调用时即时抛出。构建产出恰好包含所请求属性名的 `AttributeOwner`，构建完成后 owner 内 SHALL 一律以 AttributeName 标识属性（访问、事件、表达式引用）。

#### Scenario: 构建成功
- **WHEN** 依次向 builder 传入属性名 `strength`、`dexterity`、`attack`（均已在 spec 库登记），对 `attack` 注入表达式 `strength+0.5*dexterity`，随后调用构建
- **THEN** 得到一个可按属性名 `strength`、`dexterity`、`attack` 访问对应属性的 `AttributeOwner`，其中 `attack` 为 derived

#### Scenario: 同一属性名在不同构建中使用不同表达式
- **WHEN** 以同一属性名 `attack`（同一 spec）进行两次构建，一次注入 `strength+0.5*dexterity`，另一次注入 `intelligence*1.2`
- **THEN** 两次构建均成功，各自的 `attack` 为 derived 且按各自表达式求值

#### Scenario: 注入未请求的属性名
- **WHEN** builder 对未传入的属性名 `attack` 注入表达式并调用构建
- **THEN** 构建抛出异常，错误信息指明注入目标不在本次构建的属性名集合内

#### Scenario: 重复注入同一属性名
- **WHEN** builder 对同一属性名 `attack` 注入两次表达式并调用构建
- **THEN** 构建抛出异常，错误信息指明重复注入

#### Scenario: 注入空白表达式
- **WHEN** 注入的表达式为空字符串或纯空白字符
- **THEN** 注入调用立即抛出异常，错误信息指明表达式非法

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

### Requirement: Owner 按 AttributeName 访问属性
`AttributeOwner` SHALL 支持按属性名（AttributeName）获取属性；名称不存在时 MUST 抛出异常而非返回静默的默认值。

#### Scenario: 访问已包含的属性
- **WHEN** 在包含 `strength` 的 owner 上按 `strength` 获取属性
- **THEN** 返回对应的属性对象，可读取其当前值

#### Scenario: 访问未包含的属性
- **WHEN** 在不包含 `mana` 的 owner 上按 `mana` 获取属性
- **THEN** 抛出异常，错误信息包含 `mana`

## REMOVED Requirements

### Requirement: 构建期拒绝重复的属性名
**Reason**: spec 库改为按 AttributeName 唯一登记（同名登记被拒绝），builder 的属性名集合经去重且每个名字至多对应一个 spec，同一次构建出现重复属性名不再可能。
**Migration**: 防歧义保护前移至登记时——原"多个 spec 共享 AttributeName、按 SpecName 二选一"的用法改为登记单一 spec，由不同构建对同一属性名注入不同表达式。

## ADDED Requirements

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

## RENAMED Requirements

- FROM: `### Requirement: 按 SpecName 集合构建 Owner`
- TO: `### Requirement: 按属性名集合与表达式注入构建 Owner`
