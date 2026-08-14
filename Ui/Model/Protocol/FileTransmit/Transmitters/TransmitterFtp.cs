using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using _1RM.Service.RustFtp;

namespace _1RM.Model.Protocol.FileTransmit.Transmitters
{
    /// <summary>
    /// FTP/FTPS transmitter backed by the in-process Rust core (suppaftp via FFI).
    /// Replaces the FluentFTP implementation.
    /// </summary>
    public class TransmitterFtp : ITransmitter
    {
        public readonly string Hostname;
        public readonly int Port;
        public readonly string Username;
        public readonly string Password;
        private readonly Task _connection;
        private RustFtpBridge? _ftp = null;

        public TransmitterFtp(string host, int port, string username, string password)
        {
            Hostname = host;
            Port = port;
            Username = username;
            Password = password;
            _connection = Task.Run(() =>
            {
                _ftp = RustFtpBridge.ConnectFtp(host, (ushort)port, username, password);
            });
        }

        ~TransmitterFtp()
        {
            Release();
        }

        public async Task Conn()
        {
            await _connection;
        }

        public bool IsConnected()
        {
            return _ftp != null;
        }

        public ITransmitter Clone()
        {
            return new TransmitterFtp(Hostname, Port, Username, Password);
        }

        public async Task<RemoteItem?> Get(string path)
        {
            await Conn();
            if (_ftp == null) return null;
            return await Exists(path) ? (await ListDirectoryItems(GetParentPath(path)))
                .FirstOrDefault(x => string.Equals(x.FullName, path, StringComparison.OrdinalIgnoreCase)) : null;
        }

        private static string GetParentPath(string path)
        {
            var idx = path.LastIndexOf("/", StringComparison.Ordinal);
            return idx <= 0 ? "/" : path.Substring(0, idx);
        }

        public async Task<List<RemoteItem>> ListDirectoryItems(string path)
        {
            await Conn();
            if (_ftp == null) return new List<RemoteItem>();
            var items = _ftp.ListDirectoryItems(path);
            foreach (var item in items)
            {
                if (item.IsDirectory)
                {
                    item.Icon = TransmitItemIconCache.GetDictIcon();
                    item.FileType = "folder";
                    if (item.IsSymlink)
                        item.Icon = TransmitItemIconCache.GetDictIcon(Environment.GetFolderPath(Environment.SpecialFolder.Favorites));
                }
                else
                {
                    if (item.IsSymlink)
                        item.FileType = ".lnk";
                    if (item.Name.IndexOf(".", StringComparison.Ordinal) > 0)
                    {
                        var ext = item.Name.Substring(item.Name.LastIndexOf(".", StringComparison.Ordinal)).ToLower();
                        item.FileType = ext;
                        item.Icon = TransmitItemIconCache.GetFileIcon(ext);
                    }
                    else
                    {
                        item.Icon = TransmitItemIconCache.GetFileIcon();
                    }
                }
            }
            return items;
        }

        public async Task<bool> Exists(string path)
        {
            await Conn();
            if (_ftp == null) return false;
            try
            {
                return _ftp.Exists(path);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public async Task Delete(string path)
        {
            await Conn();
            if (_ftp == null) return;
            var item = await Get(path);
            if (item == null) return;
            _ftp.Delete(item.FullName);
        }

        public async Task Delete(RemoteItem item)
        {
            await Delete(item.FullName);
        }

        public async Task CreateDirectory(string path)
        {
            await Conn();
            if (_ftp == null) return;
            try
            {
                if (!_ftp.Exists(path))
                    _ftp.CreateDirectory(path);
            }
            catch (Exception)
            {
                // ignored: directory may already exist
            }
        }

        public async Task RenameFile(string path, string newPath)
        {
            await Conn();
            if (_ftp == null || path == newPath) return;
            if (await Exists(path))
                _ftp.RenameFile(path, newPath);
        }

        public async Task UploadFile(string localFilePath, string saveToRemotePath, Action<ulong> writeCallBack, CancellationToken cancellationToken)
        {
            var fi = new FileInfo(localFilePath);
            if (fi?.Exists != true)
                return;

            await Conn();
            if (_ftp == null) return;
            await _ftp.UploadFileAsync(localFilePath, saveToRemotePath, writeCallBack, cancellationToken);
        }

        public async Task DownloadFile(string remoteFilePath, string saveToLocalPath, Action<ulong> readCallBack, CancellationToken cancellationToken)
        {
            await Conn();
            if (_ftp == null) return;
            await _ftp.DownloadFileAsync(remoteFilePath, saveToLocalPath, readCallBack, cancellationToken);
        }

        public void Release()
        {
            var ftp = _ftp;
            _ftp = null;
            ftp?.Dispose();
        }
    }
}
