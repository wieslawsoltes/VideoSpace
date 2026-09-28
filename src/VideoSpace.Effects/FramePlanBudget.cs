namespace VideoSpace.Effects;

/// <summary>Bounds expanded graphs before serialization or GPU allocation. Shared
/// subplans count once per occurrence, because each occurrence is rendered.</summary>
public static class FramePlanBudget
{
    public const int MaximumNodes = 4096;
    public static void Validate(FramePlan plan)
    {
        int nodes = 0;
        void Layer(LayerPlan layer, int depth)
        {
            if (++nodes > MaximumNodes || depth > 24) throw new InvalidOperationException("Expanded composition exceeds 4096 nodes or 24 levels.");
            if (layer.Nested is { } child) Plan(child, depth + 1);
            if (layer.Transition is { } transition) { Layer(transition.From, depth + 1); Layer(transition.To, depth + 1); }
        }
        void Plan(FramePlan p, int depth)
        {
            nodes += p.Audio.Length + p.Captions.Length;
            if (nodes > MaximumNodes) throw new InvalidOperationException("Expanded composition exceeds 4096 nodes.");
            foreach (var layer in p.Layers) Layer(layer, depth);
        }
        Plan(plan, 0);
    }
}
