## ADDED Requirements

### Requirement: AttributeSpec 定义属性的静态构成
`AttributeSpec` SHALL 以不可变数据的形式描述一个属性的静态构成：非空的 `AttributeName`、`MinValue` 与 `MaxValue`（满足 `MinValue <= MaxValue`）。spec 只描述身份与约束，不携带计算表达式；一个属性在某次构建中是 base 还是 derived 由组装层的表达式注入决定，不属于 spec。`AttributeName` 是属性在 owner 中的名称，也是表达式中引用其它属性时使用的标识。

#### Scenario: 定义属性
- **WHEN** 创建一个含名字与最值的 spec（如 AttributeName=`strength`，Min=0，Max=999）
- **THEN** 该 spec 合法，登记后可用于构建 owner

### Requirement: 非法 spec 在构造时被拒绝
spec 在构造时 SHALL 校验自身字段，非法 spec MUST 立即抛出带有明确字段说明的异常。

#### Scenario: AttributeName 为空
- **WHEN** 创建 AttributeName 为空字符串或纯空白字符的 spec
- **THEN** 构造抛出异常，错误信息指明 AttributeName 非法

#### Scenario: 最值倒置
- **WHEN** 创建 MinValue 大于 MaxValue 的 spec
- **THEN** 构造抛出异常，错误信息指明最值关系非法

### Requirement: spec 库按 AttributeName 检索
系统 SHALL 提供 spec 库用于集中登记 `AttributeSpec`，并按 `AttributeName` 检索。`AttributeName` 在库内唯一：登记与已有 spec 同名 AttributeName 的 spec MUST 被拒绝并抛出异常——属性的身份与约束只有一个定义处，场景差异不得以复制定义的方式表达。

#### Scenario: 登记与检索
- **WHEN** 登记某个 spec 后按其 AttributeName（如 `attack`）检索
- **THEN** 返回先前登记的同一个 spec 对象

#### Scenario: 同名 AttributeName 登记被拒绝
- **WHEN** 库中已存在 AttributeName 为 `attack` 的 spec，再次登记另一个 AttributeName 同为 `attack` 的 spec
- **THEN** 抛出异常，错误信息指明 AttributeName 冲突

#### Scenario: 检索不存在的 AttributeName
- **WHEN** 按未登记的 AttributeName 检索
- **THEN** 以明确"未找到"的方式告知调用方（抛出异常或返回可判空的结果）

## REMOVED Requirements

### Requirement: AttributeSpec 定义属性
**Reason**: 定义层收敛：spec 移除 SpecName/Expression，只保留身份与约束；本条的正文与"定义 derived 属性"、"同名属性的不同算法并存"两个场景均以表达式随 spec 携带为前提，不再成立。
**Migration**: 由"AttributeSpec 定义属性的静态构成"取代；derived 之别与场景差异改经组装层表达式注入（见 attribute-owner 能力）。

### Requirement: 非法 spec 被拒绝
**Reason**: "SpecName 为空"与"表达式为空白"两个场景随字段一并消失，仅存字段（AttributeName、最值）的校验语义由新条目承载。
**Migration**: 由"非法 spec 在构造时被拒绝"取代；表达式空白的校验移至组装层的注入时机（见 attribute-owner 能力"注入空白表达式"场景）。

### Requirement: spec 库按 SpecName 检索
**Reason**: SpecName 随双名机制取消；库的键与唯一性约束改到 AttributeName 上，"共享 AttributeName 的登记被允许"被有意反转为拒绝。
**Migration**: 由"spec 库按 AttributeName 检索"取代；原"多个 spec 共享 AttributeName、按 SpecName 二选一"的用法改为登记单一 spec、不同构建注入不同表达式。
