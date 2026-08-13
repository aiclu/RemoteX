#if RUST_SSH
using System;
using System.Text;
using Microsoft.Terminal.Wpf;

namespace _1RM.Service.RustSsh
{
    /// <summary>
    /// Bridges the WPF <c>TerminalControl</c> renderer and the Rust SSH core.
    ///
    /// Implements <see cref="ITerminalConnection"/>:
    ///   - <see cref="WriteInput"/>  : terminal input (keystrokes) -> remote SSH.
    ///   - <see cref="TerminalOutput"/> : remote SSH bytes -> terminal renderer.
    ///
    /// The terminal control invokes <c>WriteInput</c> when the user types; the
    /// host pushes remote bytes into the renderer by raising <c>TerminalOutput</c>.
    /// </summary>
    internal sealed class RustSshConnection : ITerminalConnection
    {
        private readonly SshRustBridge _bridge;

        /// <summary>Fired by the terminal control with data to send to the remote.</summary>
        public event EventHandler<TerminalOutputEventArgs>? TerminalOutput;

        public RustSshConnection(SshRustBridge bridge)
        {
            _bridge = bridge;
        }

        /// <summary>Keystrokes / input from the terminal, forwarded to the remote shell.</summary>
        public void WriteInput(string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            var bytes = Encoding.UTF8.GetBytes(data);
            _bridge.Write(bytes);
        }

        /// <summary>Called by the terminal control to start the connection.</summary>
        public void Start()
        {
        }

        /// <summary>Remote PTY resize notification (rows x columns).</summary>
        public void Resize(uint rows, uint columns)
        {
            _bridge.Resize(columns, rows);
        }

        /// <summary>Called by the terminal control when it closes.</summary>
        public void Close()
        {
        }

        /// <summary>
        /// Push remote output bytes into the terminal renderer. Called by the host
        /// whenever <c>SshRustBridge.OnData</c> delivers a frame.
        /// </summary>
        public void PushRemoteData(byte[] data)
        {
            if (data == null || data.Length == 0) return;
            // TerminalControl expects UTF-16 text; convert the UTF-8 SSH stream.
            var text = Encoding.UTF8.GetString(data);
            TerminalOutput?.Invoke(this, new TerminalOutputEventArgs(text));
        }
    }
}
#endif
