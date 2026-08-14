using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;

namespace _1RM.Model.ProtocolRunner.Default
{
    /// <summary>
    /// Default runner for the Rust-backed Serial terminal (in-process, via FFI
    /// to <c>ssh_rust.dll</c>). This replaces the external PuTTY exe for Serial.
    ///
    /// This runner does NOT launch an external exe; the session is driven by
    /// <c>RustTerminalHost</c> through the FFI bridge. Only compiled/active on
    /// net9.0-windows builds.
    /// </summary>
    public class RustSerialRunner : InternalDefaultRunner
    {
        public new static string Name = "Rust Serial (Built-in)";

        [JsonConstructor]
        public RustSerialRunner(string ownerProtocolName) : base(ownerProtocolName)
        {
            base._name = Name;
        }
    }
}
