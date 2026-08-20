using System.ComponentModel.Composition;
using QuantConnect.Data.UniverseSelection;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.Server;
using QuantConnect.Packets;

namespace EmTrading.Infrastructure.Engine;

[Export(typeof(ILeanManager))]
public class NullLeanManager : ILeanManager
{
    public void Initialize(IAlgorithm algorithm, AlgorithmNodePacket job, IApi api, CancellationToken cancellationToken)
    {
    }

    public void Initialize(LeanEngineSystemHandlers systemHandlers, LeanEngineAlgorithmHandlers algorithmHandlers,
        AlgorithmNodePacket job, AlgorithmManager algorithmManager)
    {

    }

    public void SetAlgorithm(IAlgorithm algorithm)
    {
    }

    public void Update()
    {
    }

    public void OnAlgorithmStart()
    {

    }

    public void OnAlgorithmEnd()
    {
        
    }

    public void OnSecuritiesChanged(SecurityChanges changes)
    {
        
    }

    public void Dispose()
    {
    }
}