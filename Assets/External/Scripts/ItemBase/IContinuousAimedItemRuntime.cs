public interface IContinuousAimedItemRuntime
{
    bool BeginContinuousUse(in ItemUseRequest request);
    void TickContinuousUse(in ItemUseRequest request, float deltaTime);
    void EndContinuousUse();
}
