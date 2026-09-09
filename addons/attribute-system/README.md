# Attribute System

Godot 4 C# 属性系统插件（源码分发）：为游戏实体提供可订阅变更、支持表达式派生的数值层。核心组件四个：`AttributeSpec`（定义属性）→ `AttributeSpecLibrary`（注册中心）→ `AttributeOwnerBuilder`（构建）→ `AttributeOwner`（使用）。下面按完整使用流程走一遍。

## 安装

1. 把 `addons/attribute-system/` 目录复制到你的 Godot 4 C# 项目中（插件为纯 C# 代码，命名空间 `AttributeSystem`，不依赖场景树）。
2. 宿主项目 csproj 需引用表达式解析包 `NCalc`（可照抄本仓库 `attribute-system.csproj` 中的 `PackageReference`）。

## 第 1 步：配置值类型（可选）

打开 `addons/attribute-system/AttributeValueType.cs`，整个插件只有这一行声明值类型：

```csharp
global using AttributeValueType = System.Single; // 默认 float
```

改成任意数值类型（`System.Int32`、`System.Double`、`System.Decimal` 等）后重新编译宿主项目，插件所有 API 的数值类型随之切换，其它源码一行都不用动。

> 别名是编译期展开的，所以插件必须以源码方式随宿主一起编译——预编译 DLL 方式无法切换值类型。

## 第 2 步：定义并注册 AttributeSpec

每个 `AttributeSpec` 描述一种属性，创建后登记到 `AttributeSpecLibrary`：

```csharp
using AttributeSystem;

var library = new AttributeSpecLibrary();

// base 属性：给名字和区间即可（区间可选，缺省不限制）
library.Register(new AttributeSpec(
    specName: "hero-strength",       // spec 名：库内唯一，构建时按它挑定义
    attributeName: "strength",       // 属性名：运行时读写、表达式里引用都用它
    minValue: 0, maxValue: 100));

// derived 属性：多给一个 expression，值由其它属性算出
library.Register(new AttributeSpec(
    specName: "warrior-attack",
    attributeName: "attack",
    minValue: 0, maxValue: 999,
    expression: "strength+0.5*dexterity"));
```

要点：

- **attributeName 允许多个 spec 共享**——不同场景同名属性可以有不同算法，构建时按 specName 二选一：

  ```csharp
  library.Register(new AttributeSpec("mage-attack", "attack", 0, 999, expression: "intelligence*1.2"));
  ```

  但同一个 owner 里属性名不能重复（表达式按属性名解析，重名即歧义）。
- **表达式里的属性名就是它的依赖**：`strength+0.5*dexterity` 声明依赖 `strength` 和 `dexterity`，任一变化都会让该属性自动失效、下次读取时重算。除此之外没有任何依赖声明方式。
- `expression` 为 `null`（缺省）即 base 属性，提供了就是 derived 属性。

## 第 3 步：用 Builder 构建 AttributeOwner

告诉 builder 本场景需要哪些 spec，它从库里取出定义、装配成一个属性容器：

```csharp
var warrior = new AttributeOwnerBuilder(library)
    .Require("hero-strength")     // 全部用 specName
    .Require("hero-dexterity")
    .Require("warrior-attack")
    .Build();
```

同一个 library 可以构建任意多个 owner（例如战士、法师各构建一个，各自挑选自己的 attack 定义）。构建期会完成全部校验，任何问题都直接抛异常、不会带到运行期：

| 错误 | 异常信息包含 |
|---|---|
| Require 的 specName 未登记 | 缺失的 specName |
| 表达式引用了本次未构建的属性名 | 表达式原文 + 属性名 |
| 同一次构建中 attributeName 重复 | 属性名 + 冲突的两个 specName |
| 表达式循环依赖（含自引用） | 环路上的属性链 |

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
// base：值变化时收到旧值与新值；写入相同的值不触发
warrior.Get("strength").ValueChanged += (_, e) =>
    GD.Print($"{e.Name}: {e.OldValue} -> {e.NewValue}");

// derived：依赖变化时收到"失效"通知。
// e.NewValue 是惰性的——处理器里访问它才触发重算，不访问不求值
warrior.Get("attack").ValueChanged += (_, e) =>
    GD.Print($"attack 已失效，新值是 {e.NewValue}");
```

说明：

- 通知按依赖顺序分发：`attack` 先于依赖它的 `total` 收到。
- 处理器里再做订阅/退订是安全的（当次分发不受影响）。
- 处理器抛出的异常会向上传播给触发变更的调用方。

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
- spec 的 min/max 是硬性安全护栏（防负数、防溢出），写入与派生结果都会被钳制；"血量上限"这类会变化的游戏上限请建模为独立 attribute，再通过表达式参与计算（如 `Clamp(health, 0, maxHealth)`）。
- derived 的值永远等于"用依赖当前值重算"的结果，把它当只读视图即可。

## 运行测试

```bash
dotnet test
```

测试为纯逻辑测试（无需启动 Godot）；Godot 编辑器内的测试面板由 `addons/gdUnit4/` 提供，已随仓库启用。

## 本期非目标

- 修改器/modifier 桶（buff 加成聚合）
- Godot Signal 事件通道（当前为 C# `event`；需要跨语言/编辑器连接时可在其上包一层转发 Node）
- 编辑器 Inspector 配置化（spec 目前为纯代码定义）
- 多线程安全

## 文件结构

```text
addons/attribute-system/
├── AttributeValueType.cs      # 全局值类型别名（切换值类型的唯一修改点）
├── AttributeSpec.cs           # 属性定义（SpecName/AttributeName/最值/表达式）
├── AttributeSpecLibrary.cs    # spec 库（按 SpecName 登记/检索）
├── CompiledExpression.cs      # NCalc 封装：解析、依赖提取、求值转换
├── Attribute.cs               # 运行时属性 + 变更/失效事件
├── AttributeOwner.cs          # 属性容器 + 失效传播
├── AttributeOwnerBuilder.cs   # 构建器（构建期校验：缺失/未知引用/重名/循环）
├── NumberOps.cs               # 泛型数学工具（INumber 约束门槛）
└── tests/                     # gdUnit4 测试套件
```
