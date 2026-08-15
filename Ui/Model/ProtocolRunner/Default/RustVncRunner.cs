using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;

namespace _1RM.Model.ProtocolRunner.Default;

/// <summary>
/// Default runner for the Rust-backed VNC session (in-process, via FFI to
/// <c>ssh_rust.dll</c>). Does NOT launch an external exe; the session is driven
/// by <c>VncHost</c> through the RustVnc bridge.
/// </summary>
public class RustVncRunner : InternalDefaultRunner
{
    public new static string Name = "Rust VNC (Built-in)";

    [JsonConstructor]
    public RustVncRunner(string ownerProtocolName) : base(ownerProtocolName)
    {
        base._name = Name;
    }
}
