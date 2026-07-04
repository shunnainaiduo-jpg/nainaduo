# 贡献指南

感谢你的关注！欢迎提交 Issue 和 Pull Request。

## 开发环境

- Visual Studio 2022 或 VS Code
- .NET 8 SDK
- AutoCAD 2025/2026（用于测试）
- AutoCAD API DLL（编译时需要）

## 编译

```powershell
dotnet build CadLibraryManager.csproj -c Release
```

## 提交规范

使用语义化提交信息：

- `feat:` 新功能
- `fix:` 修复 Bug
- `docs:` 文档更新
- `refactor:` 重构
- `chore:` 构建/工具变动

## Pull Request

1. Fork 本仓库
2. 创建功能分支：`git checkout -b feat/my-feature`
3. 提交更改：`git commit -m "feat: 添加xxx功能"`
4. 推送分支：`git push origin feat/my-feature`
5. 创建 Pull Request