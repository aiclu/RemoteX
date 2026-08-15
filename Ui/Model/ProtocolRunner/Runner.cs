using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JsonKnownTypes;
using Newtonsoft.Json;
using _1RM.Model.ProtocolRunner.Default;
using _1RM.Service;
using Shawn.Utils;

namespace _1RM.Model.ProtocolRunner
{
    [JsonConverter(typeof(JsonKnownTypesConverter<Runner>))] // json serialize/deserialize derived types https://stackoverflow.com/a/60296886/8629624
    [JsonKnownType(typeof(Runner), nameof(Runner))]
    [JsonKnownType(typeof(ExternalRunner), nameof(ExternalRunner))]
    [JsonKnownType(typeof(InternalDefaultRunner), nameof(InternalDefaultRunner))]
    [JsonKnownType(typeof(RustSshRunner), nameof(RustSshRunner))]
    [JsonKnownType(typeof(RustTelnetRunner), nameof(RustTelnetRunner))]
    [JsonKnownType(typeof(RustSerialRunner), nameof(RustSerialRunner))]
    public class Runner : NotifyPropertyChangedBase, ICloneable
    {
        /// <summary>
        /// All discriminator values that JsonKnownTypesConverter can currently
        /// deserialize. Used to pre-filter legacy runner entries (e.g. PuttyRunner /
        /// KittyRunner removed during the Rust migration) so they are skipped
        /// silently instead of throwing JsonKnownTypesException on every startup.
        /// </summary>
        internal static readonly HashSet<string> KnownDiscriminators = new()
        {
            nameof(Runner),
            nameof(ExternalRunner),
            nameof(InternalDefaultRunner),
            nameof(RustSshRunner),
            nameof(RustTelnetRunner),
            nameof(RustSerialRunner),
        };

        public Runner(string runnerName, string ownerProtocolName)
        {
            OwnerProtocolName = ownerProtocolName;
            _name = runnerName?.Trim() ?? "";
        }

        protected string _name;
        public string Name
        {
            get => _name;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _name = "";
                    return;
                }
                var str = value;
                var invalid = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
                str = invalid.Aggregate(str, (current, c) => current.Replace(c.ToString(), ""));
                SetAndNotifyIfChanged(ref _name, str);
            }
        }

        public string OwnerProtocolName { get; set; }

        public object Clone()
        {
            return this.MemberwiseClone();
        }
    }
}
