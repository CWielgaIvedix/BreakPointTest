using NLog;
using OmniId.Hdk.Common;
using OmniId.Hdk.Gateway;
using OmniId.Hdk.Gateway.DataPackets;
using OmniId.Hdk.RfidReader;
using OmniId.Hdk.RfidReader.Commands;
using OmniId.Hdk.RfidReader.Commands.Dispatch;
using OmniId.Hdk.RfidReader.Readers;
using OmniId.Hdk.RfidReader.Readers.Impinj;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BreakPointTest
{
    class Program
    {

        enum LogGroup
        {
            /// <summary>
            /// Application
            /// </summary>
            App = 0,

            Gateways = 1,
            RFIDRdrs = 2,
            ImageFns = 3,
            Printers = 4,
            UtilityFns = 5
        }

        private const string Epc = "D9AE15000000074D";

        static void Main(string[] args)
        {
            #region Start Up

            void StartLogging()
            {
                var nlogger = LogManager.GetCurrentClassLogger();
                var mapGrpToNameSpace = new Dictionary<LogGroup, string>
                {
                    [LogGroup.App] = "OmniId.TagLab",
                    [LogGroup.Gateways] = "OmniId.Hdk.Gateway",
                    [LogGroup.RFIDRdrs] = "OmniId.Hdk.RfidReader",
                    [LogGroup.ImageFns] = "OmniId.Hdk.Image",
                    [LogGroup.Printers] = "OmniId.Hdk.Printer",
                    [LogGroup.UtilityFns] = "OmniId.Hdk.Common"
                };

                LoggingSetup.FileTargetFileName = "ScanTestLogFile_${date:format=yyyyMMdd}.txt";

                // Set in initial settings into the logging facility
                LoggingSetup.FileTargetDir = "${specialfolder:folder=MyDocuments}/DeviceSampleAppLogs/";


                // Create targets and set their priority
                LoggingSetup.AddFileTarget();
                LoggingSetup.SetTargetEnable(LoggingSetup.FileTargetName,
                    true);

                // Set in priorities for each of the categories
                LoggingSetup.SetPriorityLevel(mapGrpToNameSpace[LogGroup.App],
                    OmniId.Hdk.Common.LogLevel.Trace);
                LoggingSetup.SetPriorityLevel(mapGrpToNameSpace[LogGroup.Gateways],
                    OmniId.Hdk.Common.LogLevel.Trace);
                LoggingSetup.SetPriorityLevel(mapGrpToNameSpace[LogGroup.RFIDRdrs],
                    OmniId.Hdk.Common.LogLevel.Trace);
                LoggingSetup.SetPriorityLevel(mapGrpToNameSpace[LogGroup.ImageFns],
                    OmniId.Hdk.Common.LogLevel.Trace);
                LoggingSetup.SetPriorityLevel(mapGrpToNameSpace[LogGroup.Printers],
                    OmniId.Hdk.Common.LogLevel.Trace);
                LoggingSetup.SetPriorityLevel(mapGrpToNameSpace[LogGroup.UtilityFns],
                    OmniId.Hdk.Common.LogLevel.Trace);

                nlogger.Info("Starting DeviceSampleApp!");

            }
            
            StartLogging();

           
            RfidRdrMgr.Singleton.RfidRdrListChanged += OnRfidRdrListChanged;

            CmdDispatchMgr.Singleton.CmdStatusChanged += OnGtwyCmdStatusChanged;
            CmdDispatchMgr.Singleton.GrpStatusChanged += OnGrpStatusChanged;
            CmdDispatchMgr.Singleton.QueryResponse += OnQueryResponse;

            GtwyMgr.Singleton.AnnounceRcvd += OnAnnounceReceived;
            GtwyMgr.Singleton.CmdSent += CmdSend;
            GtwyMgr.Singleton.ConnectionStatus += CommectionStatus;

            RfidCmdDispatchMgr.Singleton.TagRead += OnTagRead;
            RfidCmdDispatchMgr.Singleton.CmdStatusChanged += OnRfidCmdStatusChanged;
            RfidCmdDispatchMgr.Singleton.ConnectionStatus += OnConnectionStatus;
            RfidCmdDispatchMgr.Singleton.AntennaStatus += OnAntennaStatus;
            RfidCmdDispatchMgr.Singleton.BarcodeRead += OnBarcodeRead;
            RfidCmdDispatchMgr.Singleton.ReadParamCompleted += OnReadParamCompleted;
            RfidCmdDispatchMgr.Singleton.RfidReaderException += OnRfidReaderException;
            RfidCmdDispatchMgr.Singleton.TimerTick += OnTimerTick;

            RTLSMgr.Singleton.TagLocationChanged += OnTagLocationChanged;
            RTLSMgr.Singleton.TagGtwyChanged += OnTagGtwyChanged;

            GtwyNwSvcEndPnt.Singleton.Configuration.HostSvcEndPntPort = 30005;

            bool ok = GtwyNwSvcEndPnt.Singleton.OpenHostSvcEndpt();

            if (!ok)
            {
                throw new Exception($"Unable to open host service endpoint to receive calls from the gateways:{GtwyNwSvcEndPnt.Singleton.LastErrorMessage}");
            }

            var result = RfidRdrMgr.Singleton.CreateRfidRdr(
                "10.0.0.231",
                RfidRdrType.CreateFromType(typeof(SpeedwayReader)),
                true).Result;

            if (result.ConfigurationStatus == RfidConfigurationStatusCode.Fail)
            {
                throw new Exception("what does this mean?!");
            }

            var reader = result.GetReader();

            var gateway = GtwyMgr.Singleton.CreateGtwy(
                typeof(NetworkGtwy),
                true,
                out var errMsg,
                "10.0.0.230",
                "80");

            if (!string.IsNullOrEmpty(errMsg))
            {
                throw new Exception($"error connecting to {gateway}: {errMsg}");
            }

            #endregion

            #region Scan

            var scanCommand = new ScanRfidCmd(null);
            
            scanCommand.Bind(null, reader);

            var success = RfidCmdDispatchMgr.Singleton.SendQueuedCmd(scanCommand);
            if (!string.IsNullOrEmpty(success))
            {
                throw new Exception(success);
            }

            #endregion

            while (true) {
                Task.Delay(1000).Wait();
            }
        }

        private static void OnTagGtwyChanged(object sender, ITagLocationEventArgs e)
        {
        }

        private static void OnTagLocationChanged(object sender, ITagLocationEventArgs e)
        {
            Console.WriteLine($"{nameof(OnTagLocationChanged)} {e.SiteId} {e.TagUid}");
        }

        private static void OnTimerTick(object sender, TimerStepEventArgs e)
        {
        }

        private static void OnReadParamCompleted(object sender, ReadParamCompletedEventArgs e)
        {
        }

        private static void OnRfidReaderException(object sender, RfidReaderExceptionEventArgs e)
        {
            Console.WriteLine($"{nameof(OnRfidReaderException)} {e.ExcMsg}");
        }

        private static void OnBarcodeRead(object sender, BarcodeReadEventArgs e)
        {
        }

        private static void OnAntennaStatus(object sender, RfidEventArgs<RfidRdrAntennaStatus> e)
        {
        }

        private static void OnRfidCmdStatusChanged(object sender, RfidCmdStatusChangedEventArgs e)
        {
            Console.WriteLine($"{nameof(OnRfidCmdStatusChanged)} {e.CallerIdNum} {e.State}");
        }

        private static void OnTagRead(object sender, TagReadEventArgs e)
        {
            // PUT BREAKPOINT HERE
            // wait for 10 seconds
            // press continue util the break point stops being hit
            // for me it is hit about 10 times and then never 10 times
            Console.WriteLine($"{nameof(OnTagRead)} {e.TagReadData.Epc}");
        }

        private static void CommectionStatus(object sender, ConnectionStatusEventArgs e)
        {
        }

        private static void CmdSend(object sender, CmdSentEventArgs e)
        {
        }

        private static void OnAnnounceReceived(object sender, AnnounceInfoEventArgs e)
        {
        }

        private static void OnQueryResponse(object sender, QueryResponseEventArgs e)
        {
        }

        private static void OnConnectionStatus(object sender, RfidEventArgs<RfidRdrConnectionStatus> e)
        {
            Console.WriteLine($"{nameof(OnConnectionStatus)} {e.Value.Rdr} {e.Value.Type}");
        }

        private static void OnGrpStatusChanged(object sender, GrpStatusChangedEventArgs e)
        {
            Console.WriteLine($"{nameof(OnGtwyCmdStatusChanged)} {e.GroupId} {e.State}");
        }

        private static void OnGtwyCmdStatusChanged(object sender, CmdStatusChangedEventArgs e)
        {
            Console.WriteLine($"{nameof(OnGtwyCmdStatusChanged)} {e.CallerIDNum} {e.State}");
        }

        private static void OnRfidRdrListChanged(object sender, DeviceListChangedEventArgs e)
        {
        }
    }
}
