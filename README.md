# Attribute System

Godot 4 C# 属性系统插件（源码分发）：为游戏实体提供可订阅变更、支持表达式派生的数值属性层。

## 简介

解决"力量 → 攻击力 → 总伤害"这类属性派生与 buff/debuff 叠加的手工维护问题：派生值漏失效、叠加规则散落各处、难以测试。本插件把属性的**定义、声明、校验、失效传播**收敛为一个小库。

特性一览：

- **表达式派生**：NCalc 表达式声明计算，依赖从表达式自动提取，依赖变更自动失效、读取时重算（惰性）
- **聚合栈**：buff/debuff、装备词缀的三桶叠加协议（flat / additive / multiplicative），每条贡献是完整表达式
- **可订阅**：base 值变更事件、derived 失效事件，按拓扑序分发
- **快速失败**：构建期一次抛齐全部声明错误；运行期变异先校验后变更，失败时状态不变
- **值类型可切换**：float → int / double / decimal 只改一行别名
- **零 Godot API 依赖**：纯 C# 逻辑库，不依赖场景树，可在无 Godot 环境下进程内测试

适用场景：Godot 4 C# 项目的角色/单位属性、buff 系统、装备词缀等数值层。

核心组件：`AttributeSpec`（定义属性）→ `AttributeSpecLibrary`（注册中心）→ `AttributeOwnerBuilder`（构建）→ `AttributeOwner`（使用），`AttributePrototypeLibrary` 按名管理可克隆的原型模板。

## 开发文档

| | |
|---|---|
| [⚙️ 架构](docs/architecture.md) | 系统分层、组件职责与关键机制 |
| [📁 结构](docs/structure.md) | 仓库布局、双工程结构与分发边界 |
| [🚀 安装详解](docs/installation.md) | 完整安装步骤、值类型切换与常见问题 |
| [🧠 技术决策](docs/decisions.md) | 设计取舍与"为什么这样做" |
| [🛠️ 开发操作](docs/DEVELOPMENT.md) | 构建、测试、覆盖率、质量扫描、发布 |

## 安装

1. 把 `addons/attribute-system/` 目录复制到你的 Godot 4 C# 项目中（插件为纯 C# 代码，命名空间 `AttributeSystem`，不依赖场景树）。
2. 宿主项目 csproj 以 ProjectReference 引用随目录分发的 `AttributeSystem.csproj`，表达式解析包 `NCalc` 随引用自动传递，无需手动添加：

   ```xml
   <ProjectReference Include="addons/attribute-system/AttributeSystem.csproj" />
   ```

3. 在宿主项目 csproj 中排除插件源码的通配编译——Godot SDK 会把 `addons/` 下所有 `.cs` 通配编进宿主程序集，与 ProjectReference 产出的 `AttributeSystem.dll` 构成重复类型（CS0436，每个类型一条警告）：

   ```xml
   <Compile Remove="addons/attribute-system/**/*.cs" />
   ```

前置要求、值类型切换细节与常见问题排查见 [docs/installation.md](docs/installation.md)。

## 第 1 步：配置值类型（可选）

打开 `addons/attribute-system/AttributeValueType.cs`，整个插件只有这一行声明值类型：

```csharp
global using AttributeValueType = System.Single; // 默认 float
```

改成任意数值类型（`System.Int32`、`System.Double`、`System.Decimal` 等）后重新编译宿主项目，插件所有 API 的数值类型随之切换，其它源码一行都不用动。

> 别名是编译期展开的，所以插件必须以源码方式随宿主一起编译——预编译 DLL 方式无法切换值类型。

## 第 2 步：定义并注册 AttributeSpec

每个 `AttributeSpec` 描述一种属性（名字 + 区间），创建后登记到 `AttributeSpecLibrary`：

```csharp
using AttributeSystem;

var library = new AttributeSpecLibrary();

// 名字 + 区间（区间可选，缺省不限制）
library.Register(new AttributeSpec("strength", 0, 100));
library.Register(new AttributeSpec("dexterity", 0, 100));
library.Register(new AttributeSpec("intelligence", 0, 100));
library.Register(new AttributeSpec("attack", 0, 999));
```

`AttributeName` 库内唯一：同名登记会被拒绝。

## 第 3 步：用 Builder 组装 AttributeOwner（属性集合 + 表达式声明）

告诉 builder 本场景需要哪些属性：只给名字就是 base，多传一个表达式就是 derived（同一个属性名在不同构建里可以用不同公式）：

```csharp
var warrior = new AttributeOwnerBuilder(library)
    .Require("strength")                          // base：只要名字
    .Require("dexterity", 6)                      // base + 初值：构建时钳制到 [min, max]
    .Require("attack", "strength+0.5*dexterity")  // derived：多传一个表达式
    .Build();

// 同一个库、同一个 attack spec，法师声明另一条公式
var mage = new AttributeOwnerBuilder(library)
    .Require("intelligence")
    .Require("attack", "intelligence*1.2")
    .Build();
```

场景公式建议收在使用方自己的静态工厂里（如 `OwnerFactory.Warrior(library)`），避免散落。构建期会完成全部校验，任何问题都直接抛异常、不会带到运行期：

| 错误 | 异常信息包含 |
|---|---|
| Require 的属性名未登记 | 缺失的属性名 |
| 同一属性名冲突声明（base/derived 混合或重复表达式） | 属性名 |
| 同一属性名声明两个不同的初值 | 属性名 |
| Require 传入空白表达式（传入时即抛） | 参数名 |
| 表达式语法错误（无法解析） | 表达式原文 |
| 表达式引用了本次未构建的属性名 | 表达式原文 + 属性名 |
| 表达式循环依赖（含自引用） | 环路上的属性链 |

同一属性名重复声明为 base 是幂等的：初值一致（含都没声明初值）等价于声明一次，只有一方声明初值时取该初值（与声明顺序无关）；其余重复声明（base/derived 混合、重复表达式、两个不同初值）一律在构建期报错。

### 用原型克隆批量生产 owner

`Build()` 的产物可以当原型：`Clone()` 得到同构成的新实例——base 属性继承原型的当前值（声明的初值即原型的初始值），derived 缓存为空、首次读取时按继承的 base 值重算，事件订阅全新，实例间互不影响。批量生产同类实体（小怪、召唤物等）时先构建原型、再逐个克隆：

```csharp
var warriorPrototype = new AttributeOwnerBuilder(library)
    .Require("strength")
    .Require("attack", "strength * 2")
    .Build();

var hero1 = warriorPrototype.Clone();
var hero2 = warriorPrototype.Clone();   // 互相独立；克隆也可继续作为原型再克隆
```

注意：克隆体与原型共享内部编译产物，**并发求值不安全**——插件所有 API 仅主线程使用。运行时注入的属性与贡献随克隆继承；变异经写时复制在克隆族之间隔离（见「聚合栈与运行时注入」）。

### 原型库：按名管理多个模板

要在创建角色等场景按名选择模板，用 `AttributePrototypeLibrary` 集中管理：配置期登记（库内执行构建、独占原型引用），创建期按名克隆。登记声明的初值随克隆带出，构建的一切错误在登记时暴露，而不是留到首次克隆时：

```csharp
var prototypes = new AttributePrototypeLibrary();

// 配置期：登记各职业模板（原型引用不出库，外部拿不到就误改不了）
prototypes.Register("warrior", new AttributeOwnerBuilder(library)
    .Require("strength", 15)
    .Require("attack", "strength * 2"));
prototypes.Register("mage", new AttributeOwnerBuilder(library)
    .Require("intelligence", 10)
    .Require("attack", "intelligence * 1.2"));

// 创建期：按名克隆，登记的初值随克隆带出（warrior 的 strength 从 15 开始）
var hero = prototypes.Clone("warrior");
prototypes.TryClone("rogue", out var rogue);      // 未登记：返回 false 而不抛异常

foreach (var name in prototypes.PrototypeNames())   // 列出全部可选模板
    GD.Print(name);
```

同名登记会被拒绝；`Contains("warrior")` 可判断模板是否存在。

## 第 4 步：使用 Owner

owner 运行时只认 attributeName：

```csharp
// base：直接写，写入值自动钳制到 [min, max]
warrior.SetValue("strength", 10);
warrior.SetValue("dexterity", 6);

// derived：只读，首次读取时计算并缓存
var attack = warrior.GetValue("attack");   // 10 + 0.5*6 = 13
// warrior.SetValue("attack", 999);        // 抛异常：derived 不可写

// 修改依赖后 derived 自动失效，下次读取用新值重算
warrior.SetValue("strength", 15);
attack = warrior.GetValue("attack");       // 15 + 3 = 18
```

需要属性对象本身时按名获取：`warrior.Get("attack")` 或 `warrior["attack"]`（名字不存在时抛异常）。

## 第 5 步：订阅变化

```csharp
// base：值变化时收到事件（发生变化的属性 + 旧值）；写入相同的值不触发
warrior.Get("strength").ValueChanged += (attribute, oldValue) =>
    GD.Print($"{attribute.Name}: {oldValue} -> {attribute.Value}");

// derived：依赖变化时收到"失效"通知，事件同样携带属性与旧值。
// 已失效（尚未重算）期间不重复通知，读取重算后再次变更会重新通知
// 新值经读取获得：读取该属性时才触发重算（惰性），不读取不求值
warrior.Get("attack").ValueChanged += (attribute, oldValue) =>
    GD.Print($"attack 已失效，旧值是 {oldValue}，新值是 {attribute.Value}");
```

说明：

- 失效通知按脏状态迁移合并：连续多次变更在下次读取前至多通知一次，旧值为失效前的缓存值。
- 通知按依赖顺序分发：`attack` 先于依赖它的 `total` 收到。
- 处理器里再做订阅/退订是安全的（当次分发不受影响）。
- 处理器抛出的异常会向上传播给触发变更的调用方。

## 聚合栈与运行时注入

需要"基础值 + 运行期可增撤的叠加项"（buff/debuff、装备词缀等）时，把属性声明为聚合栈 derived：builder 用 `RequireStacked(attributeName, seedExpression)` 声明——seed 表达式同时声明基值计算与依赖关系，聚合栈成员不提供初值，无贡献时的值就是 seed 结果的钳制值。同名冲突规则与 Require 一致：相同 seed 的重复声明幂等，不同 seed 或与 base/plain 混合声明在 Build 时报冲突。

```csharp
var owner = new AttributeOwnerBuilder(library)
    .Require("attackbase")                     // 基础攻击力（base）
    .RequireStacked("attack", "attackbase")    // 聚合栈：seed 同时声明基值计算与依赖
    .Build();

owner.SetValue("attackbase", 10);
var attack = owner.GetValue("attack");         // 无贡献时 = seed 的钳制值：10
```

### 协议

```text
attack = (seed + Σ flat) × (1 + Σ additive) × Π(1 + multiplicative)
```

- flat：平添值，直接进加法段（与 seed 同段），可为负做抵消。
- additive：加法百分比，同池求和后统一乘 (1 + 池和)；池内可正可负、相互抵消。
- multiplicative：乘法百分比，每条独立乘 (1 + m)，逐条复利、互不合并。

中间段不钳制，最终结果统一钳制到 spec 的 [MinValue, MaxValue]——additive 池和为负产生非正中间值是合法输入，由最终钳制兜底。同一桶内的贡献满足交换律：结果与投入顺序无关，系统没有顺序/优先级维度。

每条贡献是与 plain derived 表达式同等能力的完整 NCalc 表达式：可引用属性、数值常量、条件表达式（如 `hp < 30 ? 0.5 : 0`），其依赖进入聚合栈的依赖并集，依赖变化自动失效重算。

> "百分比只乘某一段"这类分层语义不用贡献表达：先声明普通 derived（如 `attackBaseBoosted = attackbase * 1.5`），聚合栈 seed 指向它——依赖图表达分段，贡献系统表达叠加。

### 运行时注入与撤销

owner 的变异原语（effect/装备系统按需调用，spec 由调用方从 spec 库取得）：

```csharp
// 运行时添加属性
owner.AddBase(new AttributeSpec("effectattackbase", 0, 100), 5);   // base + 初值（即时钳制）
owner.AddDerived(new AttributeSpec("total", 0, 999), "attack + 5");
owner.AddStacked(new AttributeSpec("burst", 0, 50), "attackbase");

// 投贡献：向聚合栈投一条 flat 贡献，返回撤销专用句柄
var handle = owner.Contribute("attack", ContributionKind.Flat, "effectattackbase");
attack = owner.GetValue("attack");         // 15 = 10 + 5，失效事件照常触发

// 撤销：句柄是一次性凭证，重复撤销、跨 owner 使用都会抛异常
owner.RemoveContribution(handle);
attack = owner.GetValue("attack");         // 10：回退 seed + 剩余贡献

// 移除属性：仍被引用时拒绝并指明引用方
owner.RemoveAttribute("effectattackbase");
```

### 与克隆的关系（写时复制）

- 首次变异前，实例与克隆族共享的内部表先自动私有化：变异不波及既有克隆，克隆上的变异也不影响原型。
- 变异后的 owner 再克隆：注入的属性与贡献随克隆继承——base 继承彼时当前值，贡献构成照搬。

### 运行时校验

与"构建期错误在 Build 抛"对应，变异的一切错误在变异调用时即抛、owner 状态不变（校验先于任何状态变更）：

| 错误 | 异常信息包含 |
|---|---|
| Add 族添加同名属性 | 属性名 |
| 表达式或 seed 语法错误（即时抛） | 表达式原文 |
| 表达式或 seed 引用未注入的属性名 | 表达式原文 + 属性名 |
| 向 base 或 plain derived 投贡献 | 目标属性名与原因 |
| 贡献构成循环依赖 | 环路上的属性链 |
| 撤销已撤销过的句柄、其它 owner 的句柄 | 原因说明 |
| RemoveAttribute 移除仍被引用的属性 | 被移除属性名 + 引用方属性名 |
| 移除不存在的属性、撤销目标已不存在 | 属性名 |
| 变异传入空白表达式或 seed（传入时即抛） | 参数名 |

## 表达式速查

```text
strength+0.5*dexterity              四则运算、括号、数值字面量
Max(attack - debuff, 0)             NCalc 内置函数：Max/Min/Abs 等
if(health < 0.2, attack*2, attack)  三元条件
total = attack + 5                  可以引用其它 derived 属性
```

- 标识符与属性名按字面精确匹配（区分大小写）。
- 整型别名下注意除法截断（`/` 按整型规则），建议用乘法与整数字面量。
- 表达式基于 NCalc 对标准数值类型求值；若别名指向 NCalc 不支持的奇异 `INumber` 实现，表达式会在转换时报错——此类项目建议不用表达式，以代码侧逻辑替代派生计算。

## 注意事项

- 所有 API 仅主线程使用（内部无锁）。
- 插件程序集保持零 Godot API 依赖：Godot 官方引擎不从引用程序集中发现 C# 脚本类，如需 `Node`/`Resource` 派生类型请放在宿主项目中实现。
- spec 的 min/max 是硬性安全护栏（防负数、防溢出），写入与派生结果都会被钳制；"血量上限"这类会变化的游戏上限请建模为独立 attribute，再通过表达式参与计算（如 `Clamp(health, 0, maxHealth)`）。
- derived 的值永远等于"用依赖当前值重算"的结果，把它当只读视图即可。

## 源码与开发

架构设计、源码结构、开发与发布流程见开发文档：

- 源码逐文件职责与仓库布局：[docs/structure.md](docs/structure.md)
- 系统分层与关键机制：[docs/architecture.md](docs/architecture.md)
- 测试（位于仓库 `tests/` 目录，gdUnit4 纯逻辑测试）、覆盖率、质量扫描与发布流程：[docs/DEVELOPMENT.md](docs/DEVELOPMENT.md)
