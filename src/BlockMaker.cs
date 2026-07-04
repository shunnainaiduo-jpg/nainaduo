using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;

namespace CadLibraryManager;

internal static class BlockMaker
{
    private const int PreviewWidth = 236;
    private const int PreviewHeight = 176;

    public static string? SaveSelectionAsDwg(string targetFolder, Func<string, string> getUniquePath)
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document == null)
        {
            return null;
        }

        var editor = document.Editor;
        if (string.IsNullOrWhiteSpace(targetFolder))
        {
            editor.WriteMessage("\n当前图库目录无效。");
            return null;
        }

        var selectionResult = editor.GetSelection(new PromptSelectionOptions
        {
            MessageForAdding = "\n选择要保存为图库图块的图元: "
        });
        if (selectionResult.Status != PromptStatus.OK || selectionResult.Value.Count == 0)
        {
            return null;
        }

        var nameResult = editor.GetString(new PromptStringOptions("\n输入图库图块名称: ")
        {
            AllowSpaces = true
        });
        if (nameResult.Status != PromptStatus.OK)
        {
            return null;
        }

        var safeName = MakeSafeFileName(nameResult.StringResult);
        if (string.IsNullOrWhiteSpace(safeName))
        {
            editor.WriteMessage("\n图块名称不能为空。");
            return null;
        }

        var pointResult = editor.GetPoint("\n指定图块基点: ");
        if (pointResult.Status != PromptStatus.OK)
        {
            return null;
        }

        Directory.CreateDirectory(targetFolder);
        var targetPath = getUniquePath(Path.Combine(targetFolder, safeName + ".dwg"));

        try
        {
            var objectIds = selectionResult.Value.GetObjectIds();
            SaveSelection(document, objectIds, pointResult.Value, targetPath);
            try
            {
                SavePreview(document, objectIds, Path.ChangeExtension(targetPath, ".png"));
            }
            catch (Exception ex)
            {
                editor.WriteMessage($"\n图块已保存，但生成缩略图失败: {ex.Message}");
            }

            editor.WriteMessage($"\n已保存到图库: {targetPath}");
            return targetPath;
        }
        catch (Exception ex)
        {
            editor.WriteMessage($"\n制作图库图块失败: {ex.Message}");
            return null;
        }
    }

    private static void SaveSelection(Document document, ObjectId[] objectIds, Point3d basePoint, string targetPath)
    {
        var ids = new ObjectIdCollection(objectIds);
        using (document.LockDocument())
        {
            using var targetDatabase = new Database(true, true);
            document.Database.Wblock(targetDatabase, ids, basePoint, DuplicateRecordCloning.Replace);
            targetDatabase.SaveAs(targetPath, DwgVersion.Current);
        }
    }

    private static void SavePreview(Document document, ObjectId[] objectIds, string previewPath)
    {
        var extents = new List<Extents3d>();
        using (var transaction = document.Database.TransactionManager.StartTransaction())
        {
            foreach (var objectId in objectIds)
            {
                if (!objectId.IsValid || objectId.IsErased)
                {
                    continue;
                }

                if (transaction.GetObject(objectId, OpenMode.ForRead, false) is not Entity entity)
                {
                    continue;
                }

                try
                {
                    extents.Add(entity.GeometricExtents);
                }
                catch
                {
                    // Some entities do not expose geometric extents until regenerated.
                }
            }

            transaction.Commit();
        }

        if (extents.Count == 0)
        {
            return;
        }

        var minX = extents.Min(extent => extent.MinPoint.X);
        var minY = extents.Min(extent => extent.MinPoint.Y);
        var maxX = extents.Max(extent => extent.MaxPoint.X);
        var maxY = extents.Max(extent => extent.MaxPoint.Y);
        if (maxX <= minX || maxY <= minY)
        {
            return;
        }

        using var bitmap = new Bitmap(PreviewWidth, PreviewHeight);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.White);

        const float padding = 14F;
        var scale = Math.Min((PreviewWidth - padding * 2) / (float)(maxX - minX), (PreviewHeight - padding * 2) / (float)(maxY - minY));
        var drawWidth = (float)(maxX - minX) * scale;
        var drawHeight = (float)(maxY - minY) * scale;
        var offsetX = (PreviewWidth - drawWidth) / 2F;
        var offsetY = (PreviewHeight - drawHeight) / 2F;

        using var borderPen = new Pen(Color.FromArgb(218, 225, 234), 1F);
        using var linePen = new Pen(Color.FromArgb(45, 77, 122), 1.6F);
        graphics.DrawRectangle(borderPen, 0, 0, PreviewWidth - 1, PreviewHeight - 1);

        foreach (var extent in extents)
        {
            var x = offsetX + (float)(extent.MinPoint.X - minX) * scale;
            var y = offsetY + (float)(maxY - extent.MaxPoint.Y) * scale;
            var width = Math.Max(1F, (float)(extent.MaxPoint.X - extent.MinPoint.X) * scale);
            var height = Math.Max(1F, (float)(extent.MaxPoint.Y - extent.MinPoint.Y) * scale);
            graphics.DrawRectangle(linePen, x, y, width, height);
        }

        bitmap.Save(previewPath, ImageFormat.Png);
    }

    private static string MakeSafeFileName(string name)
    {
        var safeName = Path.GetFileNameWithoutExtension((name ?? string.Empty).Trim());
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            safeName = safeName.Replace(invalidChar, '_');
        }

        return safeName.Trim();
    }
}
