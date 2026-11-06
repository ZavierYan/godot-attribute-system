# 开发文档

面向贡献者的开发操作手册：构建、测试、覆盖率、质量扫描、发布。

## 环境要求

- .NET SDK 8.0.x
- JDK 21（仅 SonarScanner 需要）
- Docker（仅本地 SonarQube 扫描需要，可选）

## 构建

```bash
dotnet build attribute-system.sln
```

## 运行测试

```bash
dotnet test attribute-system.sln
```

测试位于仓库根 `tests/`，为纯逻辑测试：gdUnit4 进程内运行，无需启动 Godot，无需设置 `GODOT_BIN`。

### gdUnit4 主程序集约束

gdUnit4 的 Api 包、桥接类、测试类必须全部编译进 Godot 主工程程序集（根 `attribute-system.csproj`）——gdUnit4 编辑器面板的 C# 测试发现依赖主程序集。曾尝试把测试拆分为独立 csproj（fe77716），编辑器面板测试发现随即失效，回退恢复（dea033a）。新增测试一律放在 `tests/`，不要另立测试工程。

## 覆盖率采集

```bash
dotnet tool install --global dotnet-coverage   # 已安装可跳过
mkdir -p TestResults
dotnet-coverage collect -f cobertura -o TestResults/coverage.cobertura.xml "dotnet test attribute-system.sln --no-build"
```

产出 `TestResults/coverage.cobertura.xml`，SonarScanner 消费的正是该路径（`sonar.cs.cobertura.reportsPaths`）。

## SonarCloud（CI）

CI 在 push master 与 PR 时运行「build + test + SonarCloud 扫描」（`.github/workflows/ci.yml`）：

- project key：`ZavierYan_godot-attribute-system`，org：`zavieryan`
- 仓库须配置 secret `SONAR_TOKEN`（SonarCloud 令牌），缺失时 CI 直接报错退出
- `sonar.qualityGate.wait=true`：质量门禁不通过则 CI 失败
- 覆盖率上报：`sonar.cs.cobertura.reportsPaths=TestResults/coverage.cobertura.xml`
- 扫描排除：`reports/**`

## 本地 SonarQube 扫描（可选）

用 Docker 运行一个 SonarQube 实例并在其上创建本地项目与令牌后，可复刻 CI 的扫描步骤，仅把 server 与令牌换成本地值：

1. `dotnet sonarscanner begin`（参数与 CI 一致：project key、org、`sonar.cs.cobertura.reportsPaths`、`sonar.exclusions`）
2. `dotnet build attribute-system.sln`
3. 覆盖率采集（见上节）
4. `dotnet sonarscanner end`

## 发布版本

push 形如 `v*` 的 tag 即自动发布（`.github/workflows/release.yml`），版本号以 tag 为唯一来源：

1. 守门：`dotnet build` + `dotnet test --no-build`，失败则中止、不产生 Release
2. 打包：`git archive` 打出插件目录与仓库根 README，zip 顶层为用户手册 `README.md` + `addons/attribute-system/` 前缀，命名 `attribute-system-<版本>.zip`（版本 = tag 去掉 `v` 前缀）
3. 发布：`gh release create --generate-notes --latest`
