## ADDED Requirements

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

## REMOVED Requirements

### Requirement: 按属性名集合与表达式注入构建 Owner
**Reason**: 成员资格与派生计算合并为一次 `Require(attributeName, expression)` 声明，`WithExpression` 两步式 API 移除；"注入目标未 Require"错误类随非法状态不可表达而消失，重复注入归并为统一的同名冲突声明规则。
**Migration**: `.Require(n).WithExpression(n, expr)` 改为 `.Require(n, expr)`；原"注入未请求的属性名"场景不复存在（无法写出）；原"重复注入同一属性名"场景并入"base 与 derived 混合声明被拒绝"与"同一属性名两条表达式声明被拒绝"。
