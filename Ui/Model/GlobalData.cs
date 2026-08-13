using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using _1RM.Model.Protocol.Base;
using Stylet;
using _1RM.Service;
using _1RM.Service.DataSource;
using _1RM.Service.DataSource.DAO;
using _1RM.Service.DataSource.DAO.Dapper;
using _1RM.Service.DataSource.Model;
using _1RM.Service.Locality;
using _1RM.Utils.Tracing;
using _1RM.View;
using _1RM.View.Launcher;
using Shawn.Utils;
using ServerListPageViewModel = _1RM.View.ServerView.ServerListPageViewModel;

namespace _1RM.Model
{
    public partial class GlobalData : NotifyPropertyChangedBase
    {
        public GlobalData(ConfigurationService configurationService)
        {
            InitTimer();
            _configurationService = configurationService;
        }

        private DataSourceService? _sourceService;
        private readonly ConfigurationService _configurationService;

        public void SetDataSourceService(DataSourceService sourceService)
        {
            _sourceService = sourceService;
        }


        #region Server Data

        public Action? OnReloadAll;

        public List<ProtocolBaseViewModel> VmItemList { get; set; } = new List<ProtocolBaseViewModel>();


        public ProtocolBaseViewModel? GetItemById(string dataSourceName, string serverId)
        {
            return VmItemList.FirstOrDefault(x => x.Server.DataSource?.DataSourceName == dataSourceName
                                                  && x.Id == serverId);
        }


        private int _dataVersion = 0;

        /// <summary>
        /// Check whether any data source needs a read.
        /// </summary>
        private bool NeedReload(bool force)
        {
            if (_sourceService == null)
                return false;

            var needRead = false;
            if (force == false)
            {
                needRead |= _sourceService.LocalDataSource?.NeedRead(TableServer.TABLE_NAME) ?? false;
                needRead |= _sourceService.LocalDataSource?.NeedRead(TableCredential.TABLE_NAME) ?? false;
                if (needRead == false)
                {
                    foreach (var additionalSource in _sourceService.AdditionalSources)
                    {
                        if (additionalSource.Value.Status != EnumDatabaseStatus.OK)
                        {
                            // if this source is not connected, we skip it
                            continue;
                        }

                        if (needRead == false)
                        {
                            needRead |= additionalSource.Value.NeedRead(TableServer.TABLE_NAME);
                        }
                        if (needRead == false)
                        {
                            needRead |= additionalSource.Value.NeedRead(TableCredential.TABLE_NAME);
                        }
                        if (needRead)
                        {
                            // if any additional source need read, we read all servers
                            break;
                        }
                    }
                }
            }

            return force || needRead;
        }

        /// <summary>
        /// reload data based on `LastReadFromDataSourceMillisecondsTimestamp` and `DataSourceDataUpdateTimestamp`
        /// return true if read data
        /// </summary>
        public bool ReloadAll(bool force = false)
        {
            try
            {
                if (NeedReload(force) == false)
                    return false;

                Interlocked.Increment(ref _dataVersion); // invalidate any in-flight async reload
                // read from db
                PerfTracer.Measure($"ReloadAll: DB read (force={force})", () =>
                {
                    VmItemList = _sourceService!.GetServers(force);
                    _sourceService.GetCredentials(force);
                });
                LocalityConnectRecorder.ConnectTimeCleanup();
                ReloadTagsFromServers();
                OnReloadAll?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                SimpleLogHelper.Error(ex);
                UnifyTracing.Error(ex);
            }
            return false;
        }

        /// <summary>
        /// Async reload: DB reads run on a background task; the list and UI notification are
        /// committed on the UI thread only if no newer reload has started meanwhile.
        /// </summary>
        public Task ReloadAllAsync(bool force = false)
        {
            if (NeedReload(force) == false)
                return Task.CompletedTask;

            var version = Interlocked.Increment(ref _dataVersion);
            return Task.Run(() =>
            {
                return PerfTracer.Measure($"ReloadAllAsync: DB read (force={force})", () =>
                {
                    var servers = _sourceService!.GetServers(force);
                    _sourceService.GetCredentials(force);
                    return servers;
                });
            }).ContinueWith(t =>
            {
                if (t.Exception != null)
                {
                    SimpleLogHelper.Error(t.Exception);
                    UnifyTracing.Error(t.Exception);
                    return;
                }
                if (version != Volatile.Read(ref _dataVersion))
                    return; // a newer reload superseded this one; drop the stale result

                var servers = t.Result;
                Execute.OnUIThread(() =>
                {
                    if (version != Volatile.Read(ref _dataVersion))
                        return;
                    VmItemList = servers;
                    LocalityConnectRecorder.ConnectTimeCleanup();
                    ReloadTagsFromServers();
                    OnReloadAll?.Invoke();
                });
            }, TaskScheduler.Default);
        }



        public Result AddServer(ProtocolBase protocolServer, DataSourceBase dataSource)
        {
            string info = IoC.Translate("We can not insert into database:");
            StopTick();
            if (dataSource.IsWritable == false)
            {
                return Result.Fail(info, protocolServer.DataSource, $"`{protocolServer.DataSource}` is readonly for you");
            }
            var ret = dataSource.Database_InsertServer(protocolServer);
            if (ret.IsSuccess)
            {
                ReloadAllAsync(force: true); // AddServer & needReload
            }
            StartTick();
            return ret;
        }

        public Result UpdateServer(ProtocolBase protocolServer)
        {
            return UpdateServer([protocolServer]);
        }

        public Result UpdateServer(IEnumerable<ProtocolBase> protocolServers)
        {
            StopTick();
            try
            {
                var groupedServers = protocolServers.GroupBy(x => x.DataSource);
                bool needReload = false;
                bool isAnySuccess = false;
                var failMessages = new List<string>();
                foreach (var groupedServer in groupedServers)
                {
                    var source = groupedServer.First().DataSource;
                    if (source?.IsWritable != true)
                    {
                        failMessages.Add($"Can not update on DataSource({source?.DataSourceName ?? "null"}) since it is not writable.");
                        continue;
                    }
                    needReload |= source.NeedRead(TableServer.TABLE_NAME);
                    var tmp = source.Database_UpdateServer(groupedServer);
                    isAnySuccess = tmp.IsSuccess;
                    if (!tmp.IsSuccess)
                    {
                        failMessages.Add(tmp.ErrorInfo);
                        continue;
                    }

                    if (needReload) continue;

                    // update viewmodel
                    foreach (var protocolServer in groupedServer)
                    {
                        var old = GetItemById(source.DataSourceName, protocolServer.Id);
                        // invoke main list ui change & invoke launcher ui change
                        if (old != null)
                        {
                            old.Server = protocolServer;
                            old.DataSourceNameForLauncher = _sourceService?.AdditionalSources.Any() == true ? old.DataSourceName : "";
                        }
                    }
                }

                if (isAnySuccess)
                {
                    if (needReload)
                    {
                        ReloadAllAsync(); // UpdateServers & needReload
                    }
                    else
                    {
                        ReloadTagsFromServers();
                        // TODO: 树状列表建好后，将不再有一个全局的 ServerListPageViewModel
                        IoC.Get<ServerListPageViewModel>().ClearSelection();
                    }
                }

                return failMessages.Any() ? Result.Fail(string.Join("\r\n", failMessages)) : Result.Success();
            }
            finally
            {
                StartTick();
            }
        }

        public Result DeleteServer(IEnumerable<ProtocolBase> protocolServers)
        {
            if (!protocolServers.Any()) return Result.Fail("No servers to delete.");
            StopTick();
            try
            {
                var groupedServers = protocolServers.GroupBy(x => x.DataSource);
                bool isAnySuccess = false;
                var failMessages = new List<string>();
                foreach (var groupedServer in groupedServers)
                {
                    var source = groupedServer.First().DataSource;
                    if (source?.IsWritable != true)
                    {
                        failMessages.Add($"Can not update on DataSource({source?.DataSourceName ?? "null"}) since it is not writable.");
                        continue;
                    }

                    var tmp = source.Database_DeleteServer(groupedServer.Select(x => x.Id));
                    SimpleLogHelper.DebugInfo($"DeleteServer: {string.Join(", ", groupedServer.Select(x => x.Id))}, tmp.IsSuccess = {tmp.IsSuccess}");
                    isAnySuccess = tmp.IsSuccess;
                    if (!tmp.IsSuccess)
                    {
                        failMessages.Add(tmp.ErrorInfo);
                    }
                }

                // update viewmodel
                if (isAnySuccess)
                {
                    ReloadAllAsync(true); // DeleteServers
                }

                return failMessages.Any() ? Result.Fail(string.Join("\r\n", failMessages)) : Result.Success();
            }
            catch (Exception e)
            {
                UnifyTracing.Error(e);
                throw;
            }
            finally
            {
                StartTick();
            }
        }

        #endregion Server Data


    }
}