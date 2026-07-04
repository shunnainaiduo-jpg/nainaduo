# CAD 图库管理插件

这是一个 AutoCAD 2026 C#/.NET 8 插件，用于管理本地 DWG 图库并把选中的 DWG 作为图块插入当前图纸。

## 功能

- AutoCAD 命令 `W1` 打开图库面板。
- 支持选择本地图库目录。
- 自动递归扫描目录下的 `*.dwg` 文件。
- 支持 DWG 内置缩略图预览。
- 支持按名称、分类、标签搜索。
- 支持分类、标签、显示名称、收藏。
- 支持只看收藏。
- 支持批量导入 DWG 文件。
- 支持重命名 DWG 文件并同步元数据。
- 支持设置插入比例、旋转角度、目标图层。
- 使用本地 LiteDB 数据库保存图库元数据。
- 双击或点击“插入”后，在当前图纸中指定插入点并插入图块。

## 项目结构

- `CadLibraryManager.csproj`: 插件项目文件。
- `src/Commands.cs`: AutoCAD 命令入口。
- `src/LibraryControl.cs`: 图库管理面板 UI。
- `src/BlockInserter.cs`: DWG 插入为图块的核心逻辑。
- `src/LibrarySettings.cs`: 图库目录配置保存。
- `src/LibraryDatabase.cs`: 本地元数据库访问。
- `src/DwgPreviewReader.cs`: 读取 DWG 内置缩略图。

## 编译前配置

项目默认按 AutoCAD 2026 路径查找 API DLL：

```powershell
C:\Program Files\Autodesk\AutoCAD 2026
```

如果你的 AutoCAD 安装目录不同，编译时传入 `AutoCADInstallDir`：

```powershell
dotnet build "D:\03 开发\CadLibraryManager\CadLibraryManager.csproj" -p:AutoCADInstallDir="C:\Program Files\Autodesk\AutoCAD 2025"
```

该目录需要包含：

- `AcMgd.dll`
- `AcDbMgd.dll`
- `AcCoreMgd.dll`

## 加载插件

1. 编译项目。
2. 打开 AutoCAD。
3. 执行命令 `NETLOAD`。
4. 选择编译输出的 `CadLibraryManager.dll`。
5. 执行命令 `W1` 打开图库面板。

## 自动加载安装

项目已提供 AutoCAD `.bundle` 自动加载工具，安装后 AutoCAD 启动时会自动加载插件。

默认安装到当前用户目录，不需要管理员权限。脚本会优先自动查找 `C:\Program Files\Autodesk` 下的 AutoCAD 安装目录：

```powershell
powershell -ExecutionPolicy Bypass -File "D:\03 开发\CadLibraryManager\tools\install-autoload.ps1"
```

如果自动查找失败，手动传入 AutoCAD 安装目录：

```powershell
powershell -ExecutionPolicy Bypass -File "D:\03 开发\CadLibraryManager\tools\install-autoload.ps1" -AutoCADInstallDir "C:\Program Files\Autodesk\AutoCAD 2025"
```

如果已经手动编译过，可以跳过构建，只复制现有输出：

```powershell
powershell -ExecutionPolicy Bypass -File "D:\03 开发\CadLibraryManager\tools\install-autoload.ps1" -NoBuild
```

安装到所有用户目录需要管理员权限：

```powershell
powershell -ExecutionPolicy Bypass -File "D:\03 开发\CadLibraryManager\tools\install-autoload.ps1" -Scope AllUsers -AutoCADInstallDir "C:\Program Files\Autodesk\AutoCAD 2025"
```

卸载自动加载：

```powershell
powershell -ExecutionPolicy Bypass -File "D:\03 开发\CadLibraryManager\tools\uninstall-autoload.ps1"
```

自动加载目录：

```text
%APPDATA%\Autodesk\ApplicationPlugins\CadLibraryManager.bundle
```

`.bundle` 清单文件：

```text
bundle\PackageContents.xml
```

安装完成后重启 AutoCAD，再执行 `W1` 打开图库面板。

## 图库目录

首次启动会默认使用：

```text
我的文档\CadLibraryManager
```

也可以在面板中点击“选择图库目录”切换到你的 DWG 图库文件夹。

配置保存位置：

```text
%APPDATA%\CadLibraryManager\library-folder.txt
```

元数据库保存位置：

```text
%APPDATA%\CadLibraryManager\library.db
```

## 使用方式

- “目录”：切换图库根目录。
- “刷新”：重新扫描当前图库目录。
- “导入”：批量选择外部 DWG 并复制到图库目录。
- “重命名”：重命名当前选中的 DWG 文件。
- “保存”：保存当前选中项的显示名称、分类、标签、收藏状态。
- “插入”：按当前比例、角度、图层设置插入选中的 DWG。

## 元数据字段

- 显示名称：列表中展示的名称，不要求等于文件名。
- 分类：用于按构件类型或专业分组，例如门窗、节点、家具。
- 标签：用逗号分隔，例如立面,通用,常用。
- 收藏：用于快速筛选常用图块。

## 插入设置

- 插入比例：界面按百分比输入，默认 `100` 表示原比例，`50` 表示 0.5 倍。
- 旋转角度：单位为度，默认 `0`。
- 插入图层：为空时使用当前图层；填写后如果图层不存在会自动创建。

## 已实现扩展方向

- 增加 DWG 缩略图预览。
- 增加分类、标签、收藏。
- 增加批量导入和重命名。
- 增加块比例、旋转角度、图层选择。
- 使用 LiteDB 保存图库元数据。
