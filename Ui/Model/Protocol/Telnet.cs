using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using _1RM.Model.Protocol.Base;
using _1RM.Utils;
using Shawn.Utils;

namespace _1RM.Model.Protocol
{
    public class Telnet : ProtocolBaseWithAddressPort
    {
        public static string ProtocolName = "Telnet";
        public Telnet() : base(Telnet.ProtocolName, "Putty.Telnet.V1", "Telnet")
        {
            base.Port = "23";
        }

        public override bool IsOnlyOneInstance()
        {
            return false;
        }

        public override ProtocolBase? CreateFromJsonString(string jsonString)
        {
            try
            {
                var ret = JsonConvert.DeserializeObject<Telnet>(jsonString);
                return ret;
            }
            catch (Exception e)
            {
                SimpleLogHelper.Debug(e);
                return null;
            }
        }

        public override double GetListOrder()
        {
            return 3;
        }

        private string _startupAutoCommand = "";

        public string StartupAutoCommand
        {
            get => _startupAutoCommand;
            set => SetAndNotifyIfChanged(ref _startupAutoCommand, value);
        }

        private int _encodingCodePage = 65001; // UTF-8
        public int EncodingCodePage
        {
            get => _encodingCodePage;
            set => SetAndNotifyIfChanged(ref _encodingCodePage, value);
        }

        /// <summary>Supported code pages for decoding the telnet byte stream.</summary>
        [JsonIgnore]
        public Dictionary<int, string> CodePages => EnumEncodingHelper.SupportedCodePages;
    }
}