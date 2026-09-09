# derived-attributes Specification

## Purpose

定义 derived 属性的求值模型：依赖关系由表达式自身声明（标识符即依赖属性的属性名），表达式经 NCalc 解析为计算委托并在构建期装配进属性（即"注入计算方法"）、结果受最值钳制、缓存懒加载（一次失效至多重算一次）、以及 base 变化沿依赖图的失效传播。

## Requirements

### Requirement: 表达式同时声明计算方法与依赖关系
derived 属性的依赖关系 SHALL 完全由其表达式中的属性名标识符决定：表达式中出现的每个属性名（AttributeName）即一个依赖，除表达式外不存在其它依赖声明途径。表达式中的标识符与属性名按字面精确匹配。

#### Scenario: 从表达式得出依赖集合
- **WHEN** `attack` 的表达式为 `strength+0.5*dexterity`
- **THEN** `attack` 的依赖集合为 {`strength`, `dexterity`}，二者任一变化都会使其缓存失效，其它属性变化不会

### Requirement: 表达式求值
derived 属性的值 SHALL 由其 spec 表达式在引用属性的当前值上求值得出，支持四则运算、括号、数值字面量与 NCalc 内置函数（如 `Max`、`Min`、`Abs`、`Clamp` 风格函数）；求值结果 MUST 转换为 `AttributeValueType`。

#### Scenario: 简单算术表达式
- **WHEN** `attack` 的表达式为 `strength+0.5*dexterity`，`strength` 为 10、`dexterity` 为 6
- **THEN** 读取 `attack` 得到 13

#### Scenario: 引用另一个 derived 属性
- **WHEN** `total` 的表达式为 `attack + defense`，其中 `attack` 自身是表达式为 `strength * 2` 的 derived 属性，`strength` 为 10、`defense` 为 5
- **THEN** 读取 `total` 得到 25

#### Scenario: 使用内置函数
- **WHEN** `netAttack` 的表达式为 `Max(attack - debuff, 0)`，`attack` 为 10、`debuff` 为 15
- **THEN** 读取 `netAttack` 得到 0

### Requirement: 缓存懒加载
derived 属性的值 SHALL 在首次读取时才计算并缓存；在缓存未失效期间重复读取 MUST 返回缓存值，不得重复求值；失效后的下一次读取重新求值一次。

#### Scenario: 未失效期间只求值一次
- **WHEN** derived 属性被连续读取两次且期间无任何依赖变化
- **THEN** 求值仅发生一次，两次读取返回相同结果

#### Scenario: 尚未被读取时不求值
- **WHEN** owner 构建完成后、derived 属性从未被读取
- **THEN** 不发生任何求值计算

### Requirement: 失效沿依赖图传播
当某个属性的值变化时，所有（直接或间接）依赖它的 derived 属性的缓存 MUST 被标记为失效；这些 derived 在失效后的首次读取时用新的依赖值重新求值。

#### Scenario: base 变化后重算
- **WHEN** `attack` 表达式为 `strength * 2` 且已被读取（值 20），随后 `strength` 改为 15
- **THEN** 再次读取 `attack` 得到 30

#### Scenario: 级联失效
- **WHEN** `attack = strength * 2`、`total = attack + 5` 均已被读取，随后 `strength` 改为 15
- **THEN** 再次读取 `total` 得到 35

#### Scenario: 无关属性不受影响
- **WHEN** `attack` 依赖 `strength`，另有独立的 `luck` 属性；`strength` 变化
- **THEN** `luck` 的值与缓存状态不受影响

#### Scenario: 处理器异常不产生过期缓存
- **WHEN** 失效传播过程中，某个受影响 derived 属性的事件处理器抛出异常
- **THEN** 全部受影响 derived 属性的缓存已被标记为失效，失效后的读取得到重算后的新值；该异常不被系统捕获，向上传播给触发变更的调用方

### Requirement: 求值结果受最值钳制
derived 属性求值的原始结果 MUST 钳制到 spec 的 `[MinValue, MaxValue]` 区间后再缓存与返回。

#### Scenario: 表达式结果越上限
- **WHEN** `attack` 表达式为 `strength * 2`，Max=50，`strength` 为 40
- **THEN** 读取 `attack` 得到 50

#### Scenario: 表达式结果越下限
- **WHEN** `deficit` 表达式为 `vitality - 100`，`vitality` 为 0，Min=0
- **THEN** 读取 `deficit` 得到 0

### Requirement: 非法表达式在构建期暴露
语法错误或无法解析的表达式 MUST 在 owner 构建阶段抛出异常，而不是等到首次读取时。

#### Scenario: 语法错误
- **WHEN** 构建 owner 时某 spec 的表达式为 `strength *`（语法不完整）
- **THEN** 构建抛出异常，错误信息包含该表达式内容
