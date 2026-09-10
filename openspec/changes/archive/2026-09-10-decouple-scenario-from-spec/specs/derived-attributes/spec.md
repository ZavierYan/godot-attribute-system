## MODIFIED Requirements

### Requirement: 表达式求值
derived 属性的值 SHALL 由构建期为它注入的表达式在引用属性的当前值上求值得出，支持四则运算、括号、数值字面量与 NCalc 内置函数（如 `Max`、`Min`、`Abs`、`Clamp` 风格函数）；求值结果 MUST 转换为 `AttributeValueType`。

#### Scenario: 简单算术表达式
- **WHEN** `attack` 的表达式为 `strength+0.5*dexterity`，`strength` 为 10、`dexterity` 为 6
- **THEN** 读取 `attack` 得到 13

#### Scenario: 引用另一个 derived 属性
- **WHEN** `total` 的表达式为 `attack + defense`，其中 `attack` 自身是表达式为 `strength * 2` 的 derived 属性，`strength` 为 10、`defense` 为 5
- **THEN** 读取 `total` 得到 25

#### Scenario: 使用内置函数
- **WHEN** `netAttack` 的表达式为 `Max(attack - debuff, 0)`，`attack` 为 10、`debuff` 为 15
- **THEN** 读取 `netAttack` 得到 0

### Requirement: 非法表达式在构建期暴露
语法错误或无法解析的表达式 MUST 在 owner 构建阶段抛出异常，而不是等到首次读取时。

#### Scenario: 语法错误
- **WHEN** 构建 owner 时为某属性注入的表达式为 `strength *`（语法不完整）
- **THEN** 构建抛出异常，错误信息包含该表达式内容
