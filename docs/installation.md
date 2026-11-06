# 安装

面向宿主项目使用者的完整安装说明。速览版见 [README](../README.md#安装)。

## 前置要求

- Godot 4 C#（.NET / Mono 版）宿主项目。插件是纯 C# 逻辑库，不调用 Godot API，对 Godot 小版本无要求；本仓库开发环境为 Godot 4.7.2 mono
- .NET SDK 8.0。两个 csproj 默认 `net8.0`，宿主导出 Android 时经 `GodotTargetPlatform` 条件自动切 `net9.0`，插件与主工程保持一致

## 获取插件

任选其一（结果等价，均为源码 + csproj）：

1. 从 GitHub Releases 下载 `attribute-system-<版本>.zip`，解压得到 `addons/attribute-system/`
2. 直接复制仓库的 `addons/attribute-system/` 目录

## 接入宿主项目（3 步）

1. 把 `addons/attribute-system/` 目录复制到你的 Godot 4 C# 项目中（插件为纯 C# 代码，命名空间 `AttributeSystem`，不依赖场景树）。
2. 宿主项目 csproj 以 ProjectReference 引用随目录分发的 `AttributeSystem.csproj`，表达式解析包 `NCalc` 随引用自动传递，无需手动添加：

   ```xml
   <ProjectReference Include="addons/attribute-system/AttributeSystem.csproj" />
   ```

3. 在宿主项目 csproj 中排除插件源码的通配编译——Godot SDK 会把 `addons/` 下所有 `.cs` 通配编进宿主程序集，与 ProjectReference 产出的 `AttributeSystem.dll` 构成重复类型（CS0436，每个类型一条警告）：

   ```xml
   <Compile Remove="addons/attribute-system/**/*.cs" />
   ```

## 切换值类型（可选）

打开 `addons/attribute-system/AttributeValueType.cs`，整个插件只有这一行声明值类型：

```csharp
global using AttributeValueType = System.Single; // 默认 float
```

改成任意满足 `INumber<T>` 约束的数值类型（`System.Int32`、`System.Double`、`System.Decimal` 等）后重新编译宿主项目，插件所有 API 的数值类型随之切换，其它源码一行都不用动。

> 别名是编译期展开的，所以插件必须以源码方式随宿主一起编译——预编译 DLL 方式无法切换值类型。这是源码分发的根本原因，见 [decisions.md](decisions.md)。

注意：整型别名下表达式除法按整型规则截断，建议用乘法与整数字面量；奇异 `INumber` 实现（NCalc 不支持）会在表达式转换时报错，此类项目建议不用表达式。

## 验证安装

`dotnet build` 通过且无 CS0436 警告，并跑通最小用例：

```csharp
using AttributeSystem;

var library = new AttributeSpecLibrary();
library.Register(new AttributeSpec("strength", 0, 100));

var owner = new AttributeOwnerBuilder(library)
    .Require("strength")
    .Build();

owner.SetValue("strength", 10);
// owner.GetValue("strength") == 10
```

## 常见问题

| 现象 | 原因 | 处理 |
|---|---|---|
| CS0436 重复类型警告（每个类型一条） | 宿主通配编译了插件源码，与 ProjectReference 的 dll 重复 | 补第 3 步的 `Compile Remove` |
| 找不到 `NCalc` 类型 | 只复制了源码、没有 ProjectReference——NCalc 包不会随源码自动还原 | 用 ProjectReference 引插件 csproj |
| 改了值类型别名但不生效 | 经 DLL 引用而非源码工程（别名编译期展开） | 必须以源码 ProjectReference 引用 |
| zip 里找不到测试和开发文档 | 发布 zip 只含 README + `addons/attribute-system/` | 开发内容见仓库（见 [结构](structure.md) 的分发边界） |
