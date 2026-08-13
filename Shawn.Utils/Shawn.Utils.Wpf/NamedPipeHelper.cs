using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace Shawn.Utils.Wpf
{
    public class NamedPipeHelper : IDisposable
    {
        private readonly string _pipName;
        private readonly Mutex? _singleAppMutex = null;
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        public delegate void OnMessageReceivedDelegate(string message);

        public OnMessageReceivedDelegate? OnMessageReceived;

        public NamedPipeHelper(string pipName)
        {
            this._pipName = pipName;
            _singleAppMutex = new Mutex(true, pipName, out var isFirst);
            IsServer = isFirst;
            if (isFirst)
                StartNamedPipeServer();
        }

        public bool IsServer { get; }

        public void NamedPipeSendMessage(string message)
        {
            var client = new NamedPipeClientStream(_pipName);
            try
            {
                client.Connect(1 * 1000);
                var writer = new StreamWriter(client);
                writer.WriteLine(message);
                writer.Flush();
            }
            finally
            {
                client.Close();
                client.Dispose();
            }
        }

        private void StartNamedPipeServer()
        {
            NamedPipeServerStream? server = null;
            Task.Factory.StartNew(() =>
            {
                while (!_cancellationTokenSource.IsCancellationRequested)
                {
                    server?.Dispose();
                    server = new NamedPipeServerStream(_pipName);
                    server.WaitForConnection();
                    var reader = new StreamReader(server);
                    var line = reader.ReadLine();
                    if (string.IsNullOrEmpty(line) == false)
                        OnMessageReceived?.Invoke(line);
                }
            }, _cancellationTokenSource.Token);
        }

        public void Dispose()
        {
            if (_cancellationTokenSource.IsCancellationRequested == false)
            {
                _cancellationTokenSource.Cancel(false);
            }
            _singleAppMutex?.Dispose();
            _cancellationTokenSource?.Dispose();
        }
    }
}