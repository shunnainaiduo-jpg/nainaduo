using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;

namespace CadLibraryManagerInstaller;

internal static class Program
{
    private const string BundleFolderName = "CadLibraryManager.bundle";

    [STAThread]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        try
        {
            if (Process.GetProcessesByName("acad").Length > 0)
            {
                MessageBox.Show("检测到 AutoCAD 正在运行。\n\n请先关闭 AutoCAD 后再运行安装程序。", "CAD 图库管理安装程序", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var installRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Autodesk",
                "ApplicationPlugins");
            var bundleRoot = Path.Combine(installRoot, BundleFolderName);

            if (Directory.Exists(bundleRoot))
            {
                Directory.Delete(bundleRoot, recursive: true);
            }

            Directory.CreateDirectory(installRoot);
            ExtractPayload(installRoot);

            MessageBox.Show($"安装完成。\n\n安装位置：\n{bundleRoot}\n\n请启动 AutoCAD 后输入 W1 打开图库。", "CAD 图库管理安装程序", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"安装失败：\n{ex.Message}", "CAD 图库管理安装程序", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static void ExtractPayload(string bundleRoot)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith("Payload/", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (resourceNames.Length == 0)
        {
            throw new InvalidOperationException("安装包内没有找到插件文件。请重新构建安装程序。");
        }

        foreach (var resourceName in resourceNames)
        {
            var relativePath = resourceName["Payload/".Length..].Replace('/', Path.DirectorySeparatorChar);
            var targetPath = Path.Combine(bundleRoot, relativePath);
            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrWhiteSpace(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            using var input = assembly.GetManifestResourceStream(resourceName)
                ?? throw new InvalidOperationException($"无法读取安装包资源：{resourceName}");
            using var output = File.Create(targetPath);
            input.CopyTo(output);
        }
    }
}
