# attribute-definition Specification

## Purpose

定义属性系统的静态描述层：全局值类型别名 `AttributeValueType` 的约束与切换方式，以及用 `AttributeSpec` 数据化定义一个属性的静态构成（AttributeName 与最值）并集中存放在按 AttributeName 检索、库内唯一的 spec 库中。

## Requirements

### Requirement: 全局统一的值类型别名
系统 SHALL 通过单一全局别名 `AttributeValueType` 统一所有属性相关 API 的值类型。使用方 MUST 通过修改插件中唯一的别名声明处并重新编译来切换具体类型。

#### Scenario: 默认类型下正常工作
- **WHEN** 使用方保持插件的默认值类型声明不做修改
- **THEN** 插件所有属性 API 均以该默认类型读写数值，四则运算正常执行

#### Scenario: 切换值类型
- **WHEN** 使用方将别名声明从 `float` 改为 `int` 并重新编译宿主项目
- **THEN** 插件所有属性 API 的值类型随之变为 `int`，无需修改插件其它源码

### Requirement: 值类型必须满足数值约束
别名所指向的类型 SHALL 满足 .NET 泛型数学约束（`INumber<T>` 体系）。若使用方把别名配置为不满足约束的类型，错误 MUST 在编译期暴露而非运行期。

#### Scenario: 配置为非数值类型
- **WHEN** 使用方将别名声明改为不满足数值约束的类型（如 `string`）并编译
- **THEN** 编译失败，报错指向插件内部的泛型约束检查处

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
