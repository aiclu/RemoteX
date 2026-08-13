using System;
using System.IO;
using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using _1RM.Service;

namespace _1RM.Model.ProtocolRunner.Default
{
    /// <summary>
    /// Default runner for the Rust-backed SSH terminal (in-process, via FFI to
    /// <c>ssh_rust.dll</c>). This replaces the external PuTTY exe for SSH while
    /// PuTTY remains available as an optional runner.
    ///
    /// This runner does NOT launch an external exe; the session is driven by
    /// <c>RustSshHost</c> through the FFI bridge. Only compiled/active on
    /// net9.0-windows builds.
    /// </summary>
    public class RustSshRunner : InternalDefaultRunner
    {
        public new static string Name = "Rust SSH (Built-in)";

        [JsonConstructor]
        public RustSshRunner(string ownerProtocolName) : base(ownerProtocolName)
        {
            base._name = Name;
        }
    }
}
