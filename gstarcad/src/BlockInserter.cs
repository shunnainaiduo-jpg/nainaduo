using System;
using System.IO;
using Gssoft.Gscad.ApplicationServices;
using Gssoft.Gscad.DatabaseServices;
using Gssoft.Gscad.EditorInput;
using Gssoft.Gscad.Geometry;

namespace CadLibraryManager;

internal static class BlockInserter
{
    public static void InsertDwgAsBlock(string dwgPath, InsertOptions options)
    {
        var document = Application.DocumentManager.MdiActiveDocument;
        if (document == null)
        {
            return;
        }

        var editor = document.Editor;
        if (!File.Exists(dwgPath))
        {
            editor.WriteMessage($"\nLibrary DWG file does not exist: {dwgPath}");
            return;
        }

        if (!IsValidLayerName(options.LayerName))
        {
            editor.WriteMessage($"\nLayer name contains invalid CAD characters: {options.LayerName}");
            return;
        }

        try
        {
            using var sourceDatabase = new Database(false, true);
            sourceDatabase.ReadDwgFile(dwgPath, FileOpenMode.OpenForReadAndAllShare, true, string.Empty);
            sourceDatabase.CloseInput(true);

            var blockName = MakeSafeBlockName(Path.GetFileNameWithoutExtension(dwgPath));
            ObjectId blockDefinitionId;
            using (document.LockDocument())
            {
                blockDefinitionId = GetOrImportBlockDefinition(document.Database, sourceDatabase, blockName, options.CreateUniqueBlockOnConflict);
            }

            do
            {
                var blockReference = CreateBlockReference(blockDefinitionId, options);
                var jig = new BlockInsertJig(blockReference, options.RotateOnInsert, options.RotationDegrees * Math.PI / 180.0);
                var jigStatus = jig.Drag(editor);
                if (jigStatus != PromptStatus.OK)
                {
                    blockReference.Dispose();
                    return;
                }

                var inserted = false;
                try
                {
                    InsertBlock(document, blockDefinitionId, blockReference, options);
                    inserted = true;
                }
                finally
                {
                    if (!inserted)
                    {
                        blockReference.Dispose();
                    }
                }
            }
            while (options.RepeatPlacement);
        }
        catch (Exception ex)
        {
            editor.WriteMessage($"\nInsert library block failed: {ex.Message}");
        }
    }

    private static BlockReference CreateBlockReference(ObjectId blockDefinitionId, InsertOptions options)
    {
        var blockReference = new BlockReference(Point3d.Origin, blockDefinitionId)
        {
            ScaleFactors = new Scale3d(options.Scale),
            Rotation = options.RotateOnInsert ? 0 : options.RotationDegrees * Math.PI / 180.0
        };

        return blockReference;
    }

    private static void InsertBlock(Document document, ObjectId blockDefinitionId, BlockReference blockReference, InsertOptions options)
    {
        var targetDatabase = document.Database;
        using (document.LockDocument())
        {
            using (var transaction = targetDatabase.TransactionManager.StartTransaction())
            {
                var currentSpace = (BlockTableRecord)transaction.GetObject(targetDatabase.CurrentSpaceId, OpenMode.ForWrite);
                if (!options.UseCurrentLayer && !string.IsNullOrWhiteSpace(options.LayerName))
                {
                    EnsureLayer(transaction, targetDatabase, options.LayerName);
                    blockReference.Layer = options.LayerName;
                }

                currentSpace.AppendEntity(blockReference);
                transaction.AddNewlyCreatedDBObject(blockReference, true);
                var blockDefinition = (BlockTableRecord)transaction.GetObject(blockDefinitionId, OpenMode.ForWrite);
                var originalExplodable = blockDefinition.Explodable;
                blockDefinition.Explodable = options.InsertAsBlock ? options.AllowExplode : true;
                AddAttributeReferences(transaction, blockDefinitionId, blockReference, options);

                if (!options.InsertAsBlock)
                {
                    ExplodeBlockReference(transaction, currentSpace, blockReference);
                    blockDefinition.Explodable = originalExplodable;
                    transaction.Commit();
                    return;
                }

                transaction.Commit();
            }
        }
    }

    private sealed class BlockInsertJig : EntityJig
    {
        private readonly BlockReference _blockReference;
        private readonly bool _rotateOnInsert;
        private readonly double _defaultRotationRadians;
        private Point3d _position = Point3d.Origin;
        private double _rotationRadians;
        private bool _acquiringRotation;

        public BlockInsertJig(BlockReference blockReference, bool rotateOnInsert, double defaultRotationRadians)
            : base(blockReference)
        {
            _blockReference = blockReference;
            _rotateOnInsert = rotateOnInsert;
            _defaultRotationRadians = defaultRotationRadians;
            _rotationRadians = rotateOnInsert ? 0 : defaultRotationRadians;
        }

        public PromptStatus Drag(Editor editor)
        {
            var pointResult = editor.Drag(this);
            if (pointResult.Status != PromptStatus.OK || !_rotateOnInsert)
            {
                return pointResult.Status;
            }

            _acquiringRotation = true;
            _rotationRadians = _defaultRotationRadians;
            _blockReference.Rotation = _rotationRadians;
            return editor.Drag(this).Status;
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            if (_acquiringRotation)
            {
                return SampleRotation(prompts);
            }

            return SamplePosition(prompts);
        }

        protected override bool Update()
        {
            _blockReference.Position = _position;
            _blockReference.Rotation = _rotationRadians;
            return true;
        }

        private SamplerStatus SamplePosition(JigPrompts prompts)
        {
            var options = new JigPromptPointOptions("\nSpecify insertion point: ")
            {
                UserInputControls = UserInputControls.Accept3dCoordinates
            };
            var result = prompts.AcquirePoint(options);
            if (result.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            if (result.Value.IsEqualTo(_position))
            {
                return SamplerStatus.NoChange;
            }

            _position = result.Value;
            return SamplerStatus.OK;
        }

        private SamplerStatus SampleRotation(JigPrompts prompts)
        {
            var options = new JigPromptAngleOptions($"\nSpecify rotation angle <{_defaultRotationRadians * 180.0 / Math.PI:0.##}>: ")
            {
                BasePoint = _position,
                UseBasePoint = true,
                DefaultValue = _defaultRotationRadians,
                UserInputControls = UserInputControls.Accept3dCoordinates
            };
            var result = prompts.AcquireAngle(options);
            if (result.Status == PromptStatus.None)
            {
                _rotationRadians = _defaultRotationRadians;
                return SamplerStatus.OK;
            }

            if (result.Status != PromptStatus.OK)
            {
                return SamplerStatus.Cancel;
            }

            if (Math.Abs(result.Value - _rotationRadians) < 0.0000001)
            {
                return SamplerStatus.NoChange;
            }

            _rotationRadians = result.Value;
            return SamplerStatus.OK;
        }
    }

    private static ObjectId GetOrImportBlockDefinition(Database targetDatabase, Database sourceDatabase, string blockName, bool createUniqueOnConflict)
    {
        using (var transaction = targetDatabase.TransactionManager.StartTransaction())
        {
            var blockTable = (BlockTable)transaction.GetObject(targetDatabase.BlockTableId, OpenMode.ForRead);
            if (blockTable.Has(blockName))
            {
                if (!createUniqueOnConflict)
                {
                    var blockDefinitionId = blockTable[blockName];
                    transaction.Commit();
                    return blockDefinitionId;
                }

                blockName = MakeUniqueBlockName(blockTable, blockName);
            }

            transaction.Commit();
        }

        return targetDatabase.Insert(blockName, sourceDatabase, false);
    }

    private static void AddAttributeReferences(Transaction transaction, ObjectId blockDefinitionId, BlockReference blockReference, InsertOptions options)
    {
        var blockDefinition = (BlockTableRecord)transaction.GetObject(blockDefinitionId, OpenMode.ForRead);
        foreach (ObjectId objectId in blockDefinition)
        {
            if (transaction.GetObject(objectId, OpenMode.ForRead) is not AttributeDefinition attributeDefinition || attributeDefinition.Constant)
            {
                continue;
            }

            var attributeReference = new AttributeReference();
            attributeReference.SetAttributeFromBlock(attributeDefinition, blockReference.BlockTransform);
            attributeReference.TextString = options.EditAttributes ? PromptAttributeValue(attributeDefinition) : attributeDefinition.TextString;
            blockReference.AttributeCollection.AppendAttribute(attributeReference);
            transaction.AddNewlyCreatedDBObject(attributeReference, true);
        }
    }

    private static string PromptAttributeValue(AttributeDefinition attributeDefinition)
    {
        var editor = Application.DocumentManager.MdiActiveDocument?.Editor;
        if (editor == null)
        {
            return attributeDefinition.TextString;
        }

        var defaultText = attributeDefinition.TextString ?? string.Empty;
        var promptOptions = new PromptStringOptions($"\n{attributeDefinition.Tag} <{defaultText}>: ")
        {
            AllowSpaces = true
        };
        var result = editor.GetString(promptOptions);
        if (result.Status != PromptStatus.OK)
        {
            return defaultText;
        }

        return string.IsNullOrEmpty(result.StringResult) ? defaultText : result.StringResult;
    }

    private static void ExplodeBlockReference(Transaction transaction, BlockTableRecord currentSpace, BlockReference blockReference)
    {
        var explodedObjects = new DBObjectCollection();
        blockReference.Explode(explodedObjects);
        foreach (DBObject explodedObject in explodedObjects)
        {
            if (explodedObject is not Entity entity)
            {
                explodedObject.Dispose();
                continue;
            }

            currentSpace.AppendEntity(entity);
            transaction.AddNewlyCreatedDBObject(entity, true);
        }

        blockReference.Erase();
    }

    private static void EnsureLayer(Transaction transaction, Database database, string layerName)
    {
        var layerTable = (LayerTable)transaction.GetObject(database.LayerTableId, OpenMode.ForRead);
        if (layerTable.Has(layerName))
        {
            return;
        }

        layerTable.UpgradeOpen();
        var layerRecord = new LayerTableRecord { Name = layerName };
        layerTable.Add(layerRecord);
        transaction.AddNewlyCreatedDBObject(layerRecord, true);
    }

    private static bool IsValidLayerName(string layerName)
    {
        if (string.IsNullOrWhiteSpace(layerName))
        {
            return true;
        }

        var invalidChars = new[] { '<', '>', '/', '\\', '"', ':', ';', '?', '*', '|', '=', '`' };
        return layerName.IndexOfAny(invalidChars) < 0;
    }

    private static string MakeSafeBlockName(string name)
    {
        foreach (var invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "LibraryBlock" : name;
    }

    private static string MakeUniqueBlockName(BlockTable blockTable, string baseName)
    {
        var index = 1;
        string candidate;
        do
        {
            candidate = $"{baseName}_{index}";
            index++;
        }
        while (blockTable.Has(candidate));

        return candidate;
    }
}

