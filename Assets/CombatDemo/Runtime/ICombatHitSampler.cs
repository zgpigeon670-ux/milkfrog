namespace Milkfrog.CombatDemo
{
    // Normal actors use baked sword traces; creatures may supply their own geometry.
    public interface ICombatHitSampler { void Sample(float from, float to); }
}
