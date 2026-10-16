using System.Text.Json.Nodes;

namespace SolYan.CorelAI.Corel;

public sealed partial class CorelSession
{
    public Task<JsonObject> RunMutationUndoSmokeTestAsync(CancellationToken cancellationToken = default) =>
        _dispatcher.InvokeAsync(() =>
        {
            dynamic app = App();
            if (app.Documents.Count == 0)
                throw new InvalidOperationException("Open a CorelDRAW document before running the mutation/undo smoke test.");
            if (_commandGroupOpen)
                throw new InvalidOperationException("Cannot run the smoke test while another SolYan AI command group is active.");

            dynamic doc = app.ActiveDocument;
            dynamic page = doc.ActivePage;
            dynamic layer = doc.ActiveLayer;
            var shapesBefore = Convert.ToInt32(page.FindShapes().Count);
            var dirtyBefore = TryBool(() => doc.Dirty);
            var groupOpen = false;
            var shapeCreated = false;
            object? createdShape = null;
            int? staticId = null;

            try
            {
                doc.BeginCommandGroup("SolYan AI Mutation Smoke Test");
                groupOpen = true;

                var size = ToDocumentUnits(app, doc, 5d);
                var centerX = -Convert.ToDouble(doc.DrawingOriginX);
                var centerY = -Convert.ToDouble(doc.DrawingOriginY);
                dynamic shape = layer.CreateRectangle2(
                    centerX - size / 2d,
                    centerY - size / 2d,
                    size,
                    size);
                createdShape = shape;
                shapeCreated = true;
                staticId = TryInt(() => shape.StaticID);
                try { shape.Name = "SolYan_AI_MUTATION_SMOKE_TEST"; }
                catch { /* Naming is diagnostic metadata only. */ }

                var shapesDuring = Convert.ToInt32(page.FindShapes().Count);
                doc.EndCommandGroup();
                groupOpen = false;

                doc.Undo();
                app.Refresh();

                var shapesAfter = Convert.ToInt32(page.FindShapes().Count);
                var testShapeGone = staticId is null
                    ? shapesAfter == shapesBefore
                    : !PageContainsStaticId((object)page, staticId.Value);
                var restored = testShapeGone && shapesAfter == shapesBefore;

                return new JsonObject
                {
                    ["status"] = restored ? "pass" : "fail",
                    ["explicit_opt_in_required"] = true,
                    ["redo_history_warning"] = true,
                    ["temporary_shape_static_id"] = staticId,
                    ["shapes_before"] = shapesBefore,
                    ["shapes_during"] = shapesDuring,
                    ["shapes_after_undo"] = shapesAfter,
                    ["temporary_shape_removed"] = testShapeGone,
                    ["restored"] = restored,
                    ["dirty_before"] = dirtyBefore,
                    ["dirty_after"] = TryBool(() => doc.Dirty)
                };
            }
            catch (Exception ex)
            {
                var cleanupRemovedShape = false;

                if (shapeCreated && createdShape is not null)
                {
                    try
                    {
                        dynamic shape = createdShape;
                        shape.Delete();
                        cleanupRemovedShape = true;
                    }
                    catch
                    {
                        // Fall through to the command-group cleanup attempt below.
                    }
                }

                if (groupOpen)
                {
                    try { doc.EndCommandGroup(); }
                    catch { /* Preserve the original failure. */ }
                }

                try { app.Refresh(); }
                catch { /* Preserve the original failure. */ }

                throw new InvalidOperationException(
                    $"Mutation/undo smoke test failed. Temporary-shape cleanup attempted: {shapeCreated}; cleanup removed shape: {cleanupRemovedShape}. {ex.Message}",
                    ex);
            }
        }, cancellationToken);

    private static bool PageContainsStaticId(object pageObject, int staticId)
    {
        dynamic page = pageObject;
        dynamic all = page.FindShapes();
        for (var i = 1; i <= Convert.ToInt32(all.Count); i++)
        {
            dynamic shape = all[i];
            if (TryInt(() => shape.StaticID) == staticId) return true;
        }
        return false;
    }
}
