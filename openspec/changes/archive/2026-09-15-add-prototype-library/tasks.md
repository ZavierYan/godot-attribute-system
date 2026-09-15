## 1. 源码：Require 初值声明

- [x] 1.1 `AttributeOwnerBuilder` 新增 `Require(string attributeName, AttributeValueType initialValue)` 重载：入口自带 `Guard.NotBlank(attributeName)`（校验在公共重载入口、`AddRequired` 内部不做，沿既有模式）；私有 `AddRequired` 扩展初值参数并保留既有混合声明检查（design D2）；重复声明按 design D2 规则（一致/仅一方有值→幂等或合并，两方不同显式初值→记入 `_conflictedNames`，一致性比较经 `EqualityComparer<T>.Default`）；Build 错误信息措辞覆盖矛盾初值。验证：`dotnet build` 仅既有 CS8600 警告。
- [x] 1.2 `Attribute` 增加带初值的 base 构造路径（`CreateBase(spec, initialValue)`，经构造器写入并 `NumberOps.Clamp` 钳制，不走 Value setter）；builder 实例化处按 `_initialValuesByAttributeName` 取初值。验证：新增单测「初值生效」「越界初值被钳制」通过。

## 2. 源码：克隆语义 X

- [x] 2.1 `Attribute.CloneAttribute()` 分叉：base 复制当前 `_value`，derived 维持全新（缓存空）。同步 `AttributeOwner.Clone()`、`Attribute` 类注释中的契约措辞（"继承 base 当前值"）。验证：克隆相关测试反转/新增后全绿。
- [x] 2.2 既有克隆用例适配（X 语义下必破的四例全改）：AttributeOwnerCloneTest 中"克隆从初始值开始"断言反转为继承当前值；"克隆可再克隆"按继承语义改写；`CloneCachesAreIndependent` 与 `InvalidationIsIsolatedPerInstance` 的 `attack` 断言 0 改为按继承的 `strength` 重算值（连同失效注释）。验证：`dotnet test` 无遗留失败。

## 3. 源码：AttributePrototypeLibrary

- [x] 3.1 新建 `AttributePrototypeLibrary`：`Register(name, builder)`（`Guard.NotBlank` 原型名、null builder 抛 `ArgumentNullException`、同名拒绝、内部 `builder.Build()` 私有持有）、`Clone(name)`（缺失抛 KeyNotFoundException）、`TryClone`（`bool + out`）、`Contains`、`PrototypeNames`（快照）；比较器沿用 `AttributeNames.Comparer`。验证：库单测全绿。

## 4. 测试（65 → 约 77）

- [x] 4.1 AttributeOwnerTest 增初值声明四例：初值生效、越界初值被钳制、同一属性名两个不同初值 Build 抛（错误信息含属性名）、无值+有值合并取显式值（两种顺序各断言一次）。验证：四例通过。
- [x] 4.2 AttributeOwnerCloneTest 增/改三例：克隆继承原型 base 当前值（2.2 已反转）、克隆的 derived 从继承 base 值惰性重算、克隆可再克隆继承彼时当前值。验证：全绿。
- [x] 4.3 新建 AttributePrototypeLibraryTest 七例对应 delta 场景：登记后按名克隆（初值随克隆带出）、同名登记拒绝、登记空白原型名即时抛、克隆未登记名抛、尝试克隆与存在判断、构建错误在登记时暴露、原型名枚举。验证：全绿且 `dotnet test` 总数与新增数一致。

## 5. README

- [x] 5.1 组装节：`Require(name, initialValue)` 示例一行；构建期校验表补"同一属性名两个不同的初值声明"一行；重复声明幂等句补初值合并语义。验证：通读无设计信息泄漏（沿用户化标准）。
- [x] 5.2 克隆节：措辞改为"克隆继承原型 base 属性的当前值；derived 缓存为空照常惰性重算"，并发警告保留。验证：与规格场景一致。
- [x] 5.3 新增"原型库"一节：配置期 `Register` → 创建期 `Clone(name)` / `PrototypeNames` 示例，注明原型不暴露、登记时暴露一切构建错误；同步更新开篇核心组件枚举（四个 → 五个，补 `AttributePrototypeLibrary`）。验证：示例可编译。

## 6. 规格同步与归档

- [x] 6.1 主规格 Purpose 直改：attribute-owner 的 Purpose 补"构建完成的实例可作为原型克隆出新实例，原型可按名登记于原型库"一句。验证：`openspec validate --specs --strict`。
- [x] 6.2 sync delta 至主规格（MODIFIED 构建 requirement 十场景、REMOVED 旧克隆 requirement、ADDED 两条新 requirement），tasks 全部勾选后归档至 `YYYY-MM-DD-add-prototype-library`。验证：`openspec validate <归档名> --archived --strict` 通过、旧标题「prototype 克隆」在主规格零残留。
