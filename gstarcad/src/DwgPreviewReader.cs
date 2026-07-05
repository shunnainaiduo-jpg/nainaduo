using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.Geometry;

namespace CadLibraryManager;

internal static class DwgPreviewReader
{
    private const int PreviewWidth = 236;
    private const int PreviewHeight = 176;

    public static System.Drawing.Image? ReadPreview(string dwgPath)
    {
        if (!File.Exists(dwgPath))
        {
            return null;
        }

        var sidecarImage = ReadSidecarPreview(dwgPath);
        if (sidecarImage != null)
        {
            return sidecarImage;
        }

        try
        {
            using var database = new Database(false, true);
            database.ReadDwgFile(dwgPath, FileOpenMode.OpenForReadAndAllShare, true, string.Empty);
            database.CloseInput(true);
            var thumbnail = database.ThumbnailBitmap;
            if (thumbnail == null) return null;
            try { return new Bitmap(thumbnail); }
            finally { thumbnail.Dispose(); }
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ReadPreview failed: {ex.Message}");
            return null;
        }
    }

    public static System.Drawing.Image? ReadSidecarPreview(string dwgPath)
    {
        var sidecarPreview = Path.ChangeExtension(dwgPath, ".png");
        if (File.Exists(sidecarPreview))
        {
            try
            {
                using var image = System.Drawing.Image.FromFile(sidecarPreview);
                return new Bitmap(image);
            }
            catch
            {
            }
        }

        return null;
    }

    public static bool GeneratePreviewPng(string dwgPath, bool overwrite)
    {
        if (!File.Exists(dwgPath))
        {
            return false;
        }

        var previewPath = Path.ChangeExtension(dwgPath, ".png");
        if (!overwrite && File.Exists(previewPath))
        {
            return false;
        }

        try
        {
            using var bitmap = CreatePreviewBitmap(dwgPath);
            if (bitmap == null)
            {
                return false;
            }

            bitmap.Save(previewPath, ImageFormat.Png);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static Bitmap? CreatePreviewBitmap(string dwgPath)
    {
        using var database = new Database(false, true);
        database.ReadDwgFile(dwgPath, FileOpenMode.OpenForReadAndAllShare, true, string.Empty);
        database.CloseInput(true);

        var thumbnail = database.ThumbnailBitmap;
        if (thumbnail != null)
        {
            try
            {
                return ResizeToPreview(thumbnail);
            }
            finally
            {
                thumbnail.Dispose();
            }
        }

        return CreateExtentsPreview(database);
    }

    private static Bitmap ResizeToPreview(Bitmap source)
    {
        var bitmap = new Bitmap(PreviewWidth, PreviewHeight);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.Clear(Color.White);

        var scale = Math.Min((float)PreviewWidth / source.Width, (float)PreviewHeight / source.Height);
        var width = source.Width * scale;
        var height = source.Height * scale;
        var x = (PreviewWidth - width) / 2F;
        var y = (PreviewHeight - height) / 2F;
        graphics.DrawImage(source, x, y, width, height);
        return bitmap;
    }

    private static Bitmap? CreateExtentsPreview(Database database)
    {
        using var transaction = database.TransactionManager.StartTransaction();
        var blockTable = (BlockTable)transaction.GetObject(database.BlockTableId, OpenMode.ForRead);
        var modelSpace = (BlockTableRecord)transaction.GetObject(blockTable[BlockTableRecord.ModelSpace], OpenMode.ForRead);
        var extents = modelSpace.Cast<ObjectId>()
            .Select(objectId => TryGetExtents(transaction, objectId))
            .Where(extent => extent.HasValue)
            .Select(extent => extent!.Value)
            .ToList();
        transaction.Commit();

        if (extents.Count == 0)
        {
            return null;
        }

        var minX = extents.Min(extent => extent.MinPoint.X);
        var minY = extents.Min(extent => extent.MinPoint.Y);
        var maxX = extents.Max(extent => extent.MaxPoint.X);
        var maxY = extents.Max(extent => extent.MaxPoint.Y);
        if (maxX <= minX || maxY <= minY)
        {
            return null;
        }

        var bitmap = new Bitmap(PreviewWidth, PreviewHeight);
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

        return bitmap;
    }

    private static Extents3d? TryGetExtents(Transaction transaction, ObjectId objectId)
    {
        try
        {
            return transaction.GetObject(objectId, OpenMode.ForRead, false) is Entity entity
                ? entity.GeometricExtents
                : null;
        }
        catch
        {
            return null;
        }
    }
}

