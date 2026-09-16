## MODIFIED Requirements

### Requirement: 表达式同时声明计算方法与依赖关系
derived 属性的依赖关系 SHALL 完全由其求值所用的表达式中的属性名标识符决定——plain 形态为该属性的唯一表达式，聚合栈形态为 seed 表达式与全部贡献项表达式：表达式中出现的每个属性名（AttributeName）即一个依赖，除表达式外不存在其它依赖声明途径。表达式中的标识符与属性名按字面精确匹配；聚合栈 derived 的依赖集合 SHALL 为 seed 与全部贡献项表达式标识符的并集。

#### Scenario: 从表达式得出依赖集合
- **WHEN** `attack` 的表达式为 `strength+0.5*dexterity`
- **THEN** `attack` 的依赖集合为 {`strength`, `dexterity`}，二者任一变化都会使其缓存失效，其它属性变化不会

#### Scenario: 聚合栈的依赖并集
- **WHEN** `attack` 声明为聚合栈（seed 为 `attackbase`），并被投入 flat 贡献 `effectattackbase` 与 additive 贡献 `hp < 30 ? 0.5 : 0`
- **THEN** `attack` 的依赖集合为 {`attackbase`, `effectattackbase`, `hp`}，三者任一变化都会使其缓存失效，其它属性变化不会

## ADDED Requirements

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
