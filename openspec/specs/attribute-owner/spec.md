# attribute-owner Specification

## Purpose

定义 `AttributeOwner` 的构建协议：`AttributeOwnerBuilder` 依据所需 SpecName 集合从 spec 库取定义，以各 spec 的 AttributeName 为属性名，校验（缺失 spec、表达式未知引用、同次构建属性名重复、循环依赖）并构建出运行时按 AttributeName 访问、可读写 base 值并受最值钳制的属性容器。

## Requirements

### Requirement: 按 SpecName 集合构建 Owner
`AttributeOwnerBuilder` SHALL 接受一组 SpecName，从 spec 库逐个取得对应 `AttributeSpec`，为每个 spec 创建以其 `AttributeName` 命名的属性，构建恰好包含这些属性的 `AttributeOwner`。构建完成后，owner 内 SHALL 一律以 AttributeName 标识属性（访问、事件、表达式引用），SpecName 不参与运行时行为。

#### Scenario: 构建成功
- **WHEN** 依次向 builder 传入 SpecName `hero-strength`、`hero-dexterity`、`warrior-attack`（均已在 spec 库登记，`warrior-attack` 的表达式为 `strength+0.5*dexterity`）并调用构建
- **THEN** 得到一个可按属性名 `strength`、`dexterity`、`attack` 访问对应属性的 `AttributeOwner`

### Requirement: 构建期校验缺失的 spec
当传入的 SpecName 未在 spec 库登记时，构建 MUST 失败并抛出异常，错误信息指明缺失的 SpecName。

#### Scenario: 请求未登记的 spec
- **WHEN** builder 收到 SpecName `hero-mana`，但 spec 库中没有该 SpecName 的 spec
- **THEN** 构建抛出异常，错误信息包含 `hero-mana`

### Requirement: 构建期校验表达式的未知引用
derived spec 的表达式中引用的属性名（AttributeName）不在本次构建得到的属性名集合内时，构建 MUST 失败并抛出异常，错误信息同时指明所在表达式与未解析的属性名。

#### Scenario: 表达式引用未包含的属性
- **WHEN** 构建 SpecName `warrior-attack`（表达式 `strength+0.5*dexterity`），但本次构建的属性名集合中不含 `dexterity`
- **THEN** 构建抛出异常，错误信息同时包含 `strength+0.5*dexterity` 与 `dexterity`

### Requirement: 构建期检测循环依赖
当 derived 属性（直接或间接）依赖自身时，构建 MUST 失败并抛出异常，错误信息指明参与循环的属性链。

#### Scenario: 两个属性互相依赖
- **WHEN** 构建属性 `a`（表达式 `b + 1`）与属性 `b`（表达式 `a + 1`）
- **THEN** 构建抛出循环依赖异常，错误信息包含环路上的属性名

### Requirement: 构建期拒绝重复的属性名
同一次构建中，若两个及以上 spec 的 `AttributeName` 相同，构建 MUST 失败并抛出异常，错误信息指明冲突的属性名与涉及的 SpecName。共享 AttributeName 的不同 spec 只能分属不同的 owner 构建。

#### Scenario: 两个同属性名的 spec 同时构建
- **WHEN** 同一次构建同时包含 SpecName `warrior-attack` 与 `mage-attack`（两者 AttributeName 均为 `attack`）
- **THEN** 构建抛出异常，错误信息包含 `attack` 及两个 SpecName

#### Scenario: 同属性名的 spec 分属不同 owner
- **WHEN** 分别以 `warrior-attack` 与 `mage-attack` 为基础构建两个不同的 owner
- **THEN** 两次构建均成功，各自包含一个名为 `attack` 的属性

### Requirement: Owner 按 AttributeName 访问属性
`AttributeOwner` SHALL 支持按属性名（AttributeName）获取属性，不感知 SpecName；名称不存在时 MUST 抛出异常而非返回静默的默认值。

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
