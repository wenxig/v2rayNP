namespace ServiceLib.Models.Dto;

public enum CoreStartFailure
{
    None, Configuration, MissingCore, PortInUse, ProcessExited, Handshake, Timeout, Cancelled, Launch
}

public record CoreStartResult(CoreStartFailure Failure, string Stage, ECoreType? CoreType = null, bool PreviousChainPreserved = false)
{
    public bool Success => Failure == CoreStartFailure.None;
    public string Message => string.Format(ProtocolCapabilities.Text("MsgCoreStartResult"),
        CoreType?.ToString() ?? "", ProtocolCapabilities.Text($"CoreStage{Stage}"),
        ProtocolCapabilities.Text($"CoreFailure{Failure}"));
}
