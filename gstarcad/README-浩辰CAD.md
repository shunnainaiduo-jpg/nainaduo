# CAD 图库管理器 - 浩辰/GstarCAD 适配版

这个目录是从 AutoCAD 版迁移出的 GstarCAD 版本。

## 适配方式

- API 命名空间：`Autodesk.AutoCAD.*` -> `Gssoft.Gscad.*`
- 引用包：`GStarCad.Net 20.26.1`
- 目标框架：`net8.0-windows`
- 输出 DLL：`CadLibraryManager.GstarCAD.dll`
- 命令入口：`W1`

## 构建

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\build-gstarcad.ps1
```

构建完成后，产物会复制到：

```text
dist\GstarCAD 2026 (net8.0)\
```

## 一键安装

```powershell
powershell -ExecutionPolicy Bypass -File .\一键安装浩辰CAD版.ps1
```

脚本会：

- 构建 `CadLibraryManager.GstarCAD.dll`
- 安装到 `%APPDATA%\Gstarsoft\GstarCAD\ApplicationPlugins\CadLibraryManager.GstarCAD`
- 扫描 `HKCU:\SOFTWARE\Gstarsoft\GstarCAD` 下的浩辰/GstarCAD 配置，并写入 `Applications\CadLibraryManager.GstarCAD` 自动加载项

如果脚本提示没有找到 GstarCAD 注册表配置，请先启动一次浩辰/GstarCAD，然后重新运行安装脚本。

卸载：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\uninstall-gstarcad-autoload.ps1
```

## 在浩辰/GstarCAD 中测试

1. 启动 GstarCAD。
2. 执行 `NETLOAD`。
3. 选择 `dist\GstarCAD 2026 (net8.0)\CadLibraryManager.GstarCAD.dll`。
4. 输入命令 `W1` 打开图库面板。

## 注意

当前机器没有检测到本地浩辰/GstarCAD 安装，所以这里完成了编译级适配，尚未做 CAD 内运行验证。
如果运行时某个 API 与 AutoCAD 行为不一致，优先检查 `PaletteSet`、`EntityJig`、`Database.Insert`、`ReadDwgFile`、`Wblock` 这些交互和数据库相关路径。
