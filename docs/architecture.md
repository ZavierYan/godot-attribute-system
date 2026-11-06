# 架构

面向贡献者的系统设计说明：分层、组件职责与关键机制。用户视角的功能与用法见根目录 [README](../README.md)。

## 总体形态

纯 C# 逻辑库，命名空间 `AttributeSystem`，编译为独立的 `AttributeSystem.dll`。零 Godot API 依赖（见 [decisions.md](decisions.md)），不含场景树逻辑，全部能力经 C# API 暴露。

## 分层

```text
定义层  AttributeSpec ─── AttributeSpecLibrary
声明层  AttributeOwnerBuilder（Require / RequireStacked）
求值层  IAttributeCalculation ← DerivedCalculation（plain 表达式）
                             ← StackedCalculation（聚合栈）
        CompiledExpression ─── NCalc
运行层  AttributeOwner ─── Attribute（值 / 钳制 / 缓存 / 事件）
支撑层  AttributeTopology（拓扑序）· NumberOps<T>（泛型数学）· Guard · AttributeNames · ContributionHandle
```

## 组件职责

| 组件 | 职责 | 设计要点 |
|---|---|---|
| `AttributeSpec` | 不可变属性定义：AttributeName + [Min, Max] | 属性身份与安全区间的唯一定义处 |
| `AttributeSpecLibrary` | spec 注册中心 | 实例化而非静态全局（多套定义共存、测试隔离）；Ordinal 唯一键，同名登记拒绝 |
| `AttributeOwnerBuilder` | 流式声明 + 构建期校验 + 装配 | 三种 `Require` 重载（名字 / 名字+初值 / 名字+表达式）与 `RequireStacked`；重复声明的合并规则在此收敛 |
| `AttributeOwner` | 属性容器与运行时唯一入口 | 持四张表：`_attributes`、`_calculations`、`_directDependents`、`_topoIndex`；提供 SetValue/GetValue、变异原语（Add 族 / Contribute / RemoveContribution / RemoveAttribute）、Clone |
| `Attribute` | 单属性运行时状态 | 读写钳制、derived 惰性缓存与脏标记、ValueChanged（base=变更、derived=失效） |
| `CompiledExpression` | NCalc 表达式的构建期封装 | 构造时一次解析（语法错误在此暴露）→ AST 遍历提取标识符（依赖唯一声明来源）→ 编译为统一求值委托 |
| `IAttributeCalculation` | 求值方案统一契约 | plain 与聚合栈同签名（Dependencies + Calculator），owner 的装配、求值、环检测、引用检查对形态无感知；仅贡献守卫处做一次类型判别 |
| `DerivedCalculation` | plain derived 求值方案 | record(Dependencies, Calculator)，构建期产出、克隆时经 owner 重放绑定 |
| `StackedCalculation` | 聚合栈求值方案 | 三桶贡献 + seed；增撤贡献时全量重算依赖并集 |
| `AttributeTopology` | 计算表拓扑序构建 | DFS 拓扑排序 + 环检测（报出环路属性链）；构建期与运行期变异共用同一算法 |
| `AttributePrototypeLibrary` | 原型模板库 | 配置期登记（库内执行 Build、独占原型引用），创建期按名克隆 |
| `ContributionHandle` | 贡献撤销句柄 | 一次性凭证：重复撤销、跨 owner 使用抛异常 |
| `Guard` / `NumberOps<T>` / `AttributeNames` | 参数校验 / `INumber<T>` 泛型数学 / 比较语义收敛点 | 属性名全库统一 Ordinal 精确匹配 |

## 数据流

### 构建期（`AttributeOwnerBuilder.Build`）

1. 声明收集：`Require` 调用只登记（初值表 + 冲突集），空白表达式在声明调用时即时抛出
2. spec 解析：属性名经 `AttributeSpecLibrary` 按名取得，未登记即抛
3. 表达式编译：每个表达式包成 `CompiledExpression`——语法错误在此暴露，标识符即依赖
4. 依赖与冲突校验：引用未构建的属性名、base/derived 混合、重复初值等在 Build 一次抛齐
5. 拓扑排序：`AttributeTopology` 对计算表 DFS 排序 + 环检测
6. 装配：Attribute 实例 + 四张表 → `AttributeOwner`

声明错误一次到齐是刻意设计：Build 之后不存在声明错误（见 [decisions.md](decisions.md)）。

### 运行期写入

`SetValue` → 钳制到 [Min, Max] → 值确实变化时沿 `_directDependents` 按 `_topoIndex` 顺序传播失效（derived 置脏 + 触发失效通知）。

### 运行期读取

`GetValue` → base 直接返回；derived 若脏：收集依赖当前值 → Calculator 委托求值 → 钳制 → 写缓存、清脏。惰性：不读取不求值。

### 事件语义

- base 的 ValueChanged = 值变更（写相同值不触发）；derived 的 ValueChanged = 失效（旧值为失效前的缓存值）
- 脏状态迁移合并：两次读取之间连续变更至多通知一次
- 按拓扑序分发：依赖先于依赖它的属性收到
- 快照分发：处理器内订阅/退订不影响当次分发
- 处理器异常向上传播给触发变更的调用方

## 聚合栈协议与实现

协议（中间段不钳制，最终值统一钳制到 spec 区间）：

```text
attack = (seed + Σ flat) × (1 + Σ additive) × Π(1 + multiplicative)
```

实现要点：

- `StackedCalculation` 按贡献类型分三桶，`Compute` 三段折叠，返回**未钳制**原始值——钳制统一在 `Attribute` 执行
- 每条贡献是完整 NCalc 表达式（可用条件、可引用其它属性），其依赖并入聚合栈的依赖并集
- 增撤贡献时全量重算依赖并集，拓扑与失效传播随之更新
- 桶内满足交换律：结果与投入顺序无关，系统没有顺序/优先级维度
- "百分比只乘某一段"这类分层语义不由贡献表达：先声明普通 derived 表达分段，聚合栈 seed 指向它

## 克隆与写时复制

- 四张表与表内 List 构建后无写路径（约定不可变），克隆与原型直接共享
- `Attribute` 级克隆：base 继承当前值；derived 全新（缓存空，首次读取按克隆自身的 base 值重算）；事件订阅不继承
- 首次变异前共享表自动私有化（`_tablesSharedWithClones` 脏标记）：变异不波及既有克隆，克隆上的变异不影响原型
- 变异后的 owner 再克隆：注入的属性与贡献随克隆继承
- **并发求值不安全的根因**：`CompiledExpression.AsCalculator` 返回的委托捕获实例，每次 Evaluate 写 Parameters——同族克隆共享同一委托，并发求值互相污染。因此全库无锁、仅主线程使用

## 错误模型

两阶段快速失败：

| 阶段 | 时机 | 保证 |
|---|---|---|
| 构建期 | Build / 原型登记时一次抛齐 | 构建产物不存在声明错误 |
| 运行期 | 变异调用即抛（校验先于任何状态变更） | 失败时 owner 状态不变 |

面向使用者的错误清单见 README 的「构建期校验」与「运行时校验」两表。

## 值类型抽象

`AttributeValueType` 是 global using 别名（[AttributeValueType.cs](../addons/attribute-system/AttributeValueType.cs) 单点声明），默认 `System.Single`；`NumberOps<T>` 以 `INumber<T>` 约束提供钳制、零一常量等泛型数学。切换值类型只改别名一行——这是源码分发而非 DLL 的根本原因，见 [decisions.md](decisions.md)。表达式基于 NCalc 对标准数值类型求值，别名指向 NCalc 不支持的奇异 `INumber` 实现时在转换时报错。
