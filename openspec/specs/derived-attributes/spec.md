# derived-attributes Specification

## Purpose

定义 derived 属性的求值模型：依赖关系由表达式自身声明（标识符即依赖属性的属性名），表达式经 NCalc 解析为计算委托并在构建期装配进属性（即"注入计算方法"）、结果受最值钳制、缓存懒加载（一次失效至多重算一次）、以及 base 变化沿依赖图的失效传播。derived 除 plain 表达式形态外另支持聚合栈形态：seed 表达式声明基值，运行期以 flat / additive / multiplicative 三类类型化贡献按固定协议 (seed + Σ flat) × (1 + Σ additive) × Π(1 + multiplicative) 叠加，桶内满足交换律、贡献可增撤。

## Requirements

### Requirement: 表达式同时声明计算方法与依赖关系
derived 属性的依赖关系 SHALL 完全由其求值所用的表达式中的属性名标识符决定——plain 形态为该属性的唯一表达式，聚合栈形态为 seed 表达式与全部贡献项表达式：表达式中出现的每个属性名（AttributeName）即一个依赖，除表达式外不存在其它依赖声明途径。表达式中的标识符与属性名按字面精确匹配；聚合栈 derived 的依赖集合 SHALL 为 seed 与全部贡献项表达式标识符的并集。

#### Scenario: 从表达式得出依赖集合
- **WHEN** `attack` 的表达式为 `strength+0.5*dexterity`
- **THEN** `attack` 的依赖集合为 {`strength`, `dexterity`}，二者任一变化都会使其缓存失效，其它属性变化不会

#### Scenario: 聚合栈的依赖并集
- **WHEN** `attack` 声明为聚合栈（seed 为 `attackbase`），并被投入 flat 贡献 `effectattackbase` 与 additive 贡献 `hp < 30 ? 0.5 : 0`
- **THEN** `attack` 的依赖集合为 {`attackbase`, `effectattackbase`, `hp`}，三者任一变化都会使其缓存失效，其它属性变化不会

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
- **WHEN** 构建 owner 时为某属性注入的表达式为 `strength *`（语法不完整）
- **THEN** 构建抛出异常，错误信息包含该表达式内容

### Requirement: 聚合栈 derived 与类型化贡献
聚合栈 derived SHALL 以 seed 表达式声明基值，并在运行期接受三类类型化贡献：flat（平添值）、additive（加法百分比）与 multiplicative（乘法百分比）。其值 SHALL 按固定协议求值：`(seed + Σ flat) × (1 + Σ additive) × Π(1 + multiplicative)`，求值最终结果 MUST 钳制到 spec 的 `[MinValue, MaxValue]` 区间，中间段不钳制。seed 与每条贡献项 SHALL 各自为完整表达式，与 plain derived 的表达式同等能力（四则运算、括号、数值字面量、内置函数）。同一桶内的贡献项之间 SHALL 满足交换律：求值结果与桶内贡献的投入顺序无关；撤销任一贡献后，剩余贡献的求值结果与其历史投入顺序无关。贡献 SHALL 可在运行期撤销，撤销后按剩余贡献重新求值。

#### Scenario: 无贡献时等于 seed 的钳制值
- **WHEN** `attack` 声明为聚合栈、seed 为 `attackbase`（当前值 60，spec Max=50），尚无任何贡献
- **THEN** 读取 `attack` 得到 50（seed 结果按最值钳制）

#### Scenario: flat 贡献叠加
- **WHEN** 聚合栈 `attack` 的 seed `attackbase` 为 10，依次投入 flat 贡献 `effectattackbase`（值 5）与 `equipmentattackbase`（值 3）
- **THEN** 读取 `attack` 得到 18

#### Scenario: additive 贡献同池求和
- **WHEN** 聚合栈 `attack` 的 seed 为 100、无 flat 贡献，投入 additive 贡献 `0.5` 与 `0.25`
- **THEN** 读取 `attack` 得到 175（100 × (1 + 0.5 + 0.25)）

#### Scenario: multiplicative 贡献独立复利
- **WHEN** 聚合栈 `attack` 的 seed 为 100、无 flat 与 additive 贡献，投入 multiplicative 贡献 `0.5` 与 `0.25`
- **THEN** 读取 `attack` 得到 187.5（100 × 1.5 × 1.25）

#### Scenario: 三类贡献混合
- **WHEN** 聚合栈 `attack` 的 seed `attackbase` 为 100，投入 flat 贡献 `50`、additive 贡献 `0.25` 与 multiplicative 贡献 `0.5`
- **THEN** 读取 `attack` 得到 281.25（(100 + 50) × 1.25 × 1.5）

#### Scenario: 贡献项为动态表达式
- **WHEN** 聚合栈 `attack` 的 seed 为 100，投入 additive 贡献 `hp < 30 ? 0.5 : 0`，`hp` 当前为 25
- **THEN** 读取 `attack` 得到 150；`hp` 被写为 60 后再次读取得到 100（`hp` 变化使 `attack` 缓存失效）

#### Scenario: 桶内贡献交换律
- **WHEN** 同一组 flat 贡献（`effectattackbase` 值 5、`equipmentattackbase` 值 3）在两个同构成的 owner 上以相反顺序投入
- **THEN** 两个 owner 读取 `attack` 得到相同的值

#### Scenario: additive 池和为负时按协议求值并最终钳制
- **WHEN** 聚合栈 `attack` 的 seed 为 100、spec Min=0，投入 additive 贡献 `-0.5` 与 `-0.75`（池和 -1.25）
- **THEN** 读取 `attack` 得到 0（100 × (1 - 1.25) = -25，经最值钳制）

#### Scenario: 撤销贡献后回退
- **WHEN** 聚合栈 `attack` 的 seed `attackbase` 为 10，投入 flat 贡献（值 5 与值 3）后读取得到 18，随后撤销值为 5 的贡献
- **THEN** 再次读取 `attack` 得到 13（seed + 剩余贡献）
