# Spec Delta

## MODIFIED Requirements

### Requirement: 运行时贡献与撤销
聚合栈 derived SHALL 接受运行时类型化贡献：贡献操作指定目标属性名、贡献类型（flat / additive / multiplicative）与贡献表达式，返回可用于撤销的贡献句柄。贡献表达式 SHALL 为完整表达式，其引用的每个属性名 MUST 已存在于 owner，未解析引用与语法错误 MUST 在贡献调用时抛出。目标属性不存在、或存在但不是聚合栈 derived（base 或 plain derived）MUST 在贡献调用时抛出。贡献生效后目标及其下游沿既有依赖图失效，复用既有失效传播与事件（新值经读取该属性惰性求值）。贡献表达式引用的属性（直接或间接）依赖目标属性自身时构成循环，MUST 在贡献调用时抛出循环依赖异常。撤销贡献 SHALL 按句柄进行，撤销后目标失效并按剩余贡献重算；重复撤销同一句柄、以其它 owner 的句柄撤销 MUST 抛出。

#### Scenario: 投入贡献后失效重算
- **WHEN** 聚合栈 `attack`（seed `attackbase` 值 10）已被读取为 10，投入 flat 贡献 `effectattackbase`（值 5）后再次读取
- **THEN** 得到 15；投入使 `attack` 失效并发出失效事件（新值经读取该属性惰性求值）

#### Scenario: 贡献依赖的属性变化时失效
- **WHEN** flat 贡献 `effectattackbase` 在位，`effectattackbase` 被写为 7
- **THEN** `attack` 缓存失效，再次读取得到 seed + 7

#### Scenario: 向非聚合栈目标投贡献被拒绝
- **WHEN** 向 plain derived 属性或 base 属性投贡献
- **THEN** 贡献调用抛出异常，错误信息指明目标不接受贡献

#### Scenario: 贡献表达式引用不存在的属性
- **WHEN** 贡献表达式引用 owner 中不存在的 `ghost`
- **THEN** 贡献调用抛出异常，错误信息同时包含表达式文本与 `ghost`

#### Scenario: 贡献构成循环依赖被拒绝
- **WHEN** 聚合栈 `attack` 尚无该依赖，投入引用 `total` 的贡献，而 `total`（直接或间接）依赖 `attack`
- **THEN** 贡献调用抛出循环依赖异常，错误信息包含环路上的属性名；贡献未生效

#### Scenario: 按句柄撤销贡献
- **WHEN** 撤销一个 flat 贡献的句柄后读取目标，随后再次撤销同一句柄
- **THEN** 目标按剩余贡献重算；再次撤销抛出异常

#### Scenario: 跨 owner 的句柄被拒绝
- **WHEN** 以 owner A 上获得的贡献句柄向同构成的 owner B 发起撤销
- **THEN** 撤销调用抛出异常
