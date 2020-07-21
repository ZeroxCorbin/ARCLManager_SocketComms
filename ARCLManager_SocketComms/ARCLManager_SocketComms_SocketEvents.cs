using SocketManagerNS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ARCLManager_SocketCommsNS
{
    public partial class ARCLManager_SocketComms : GroupedTaskQueue
    {

        /// <summary>
        /// Socket has scanned a Tote.
        /// Use HealthCheckResponse() echoing the HealthCheckEventArgs with the Valid bool set correctly.
        /// </summary>
        /// <param name="sender">This will always be "this".</param>
        /// <param name="data">Command #, Tote ID, *Goal Name will be string.Empty, *Robot Number will be zero (0)</param>
        public delegate void HealthCheckEventHandler(object sender, HealthCheckEventArgs data);
        /// <summary>
        /// Socket has scanned a Tote.
        /// Use HealthCheckResponse() echoing the HealthCheckEventArgs with the Valid bool set correctly.
        /// </summary>
        public event HealthCheckEventHandler HealthCheck;

        /// <summary>
        /// Socket has been signaled that a Tote needs picked up at a goal.
        /// TODO: Need to determine response to Socket.
        /// </summary>
        /// <param name="sender">This will always be "this".</param>
        /// <param name="data">Command #, Tote ID, Goal Name, *Robot Number will be zero (0)</param>
        public delegate void ToteReadyEventHandler(object sender, SocketEventArgs data);
        /// <summary>
        /// Socket has been signaled that a Tote needs picked up at a goal.
        /// TODO: Need to determine response to Socket.
        /// </summary>
        public event ToteReadyEventHandler ToteReady;

        /// <summary>
        /// Socket has completed the loading of the Tote at the goal.
        /// No response needed.
        /// </summary>
        /// <param name="sender">This will always be "this".</param>
        /// <param name="data">Command #, Tote ID, Goal Name, Robot Number</param>
        public delegate void ToteLoadCompleteEventHandler(object sender, SocketEventArgs data);
        /// <summary>
        /// Socket has completed the loading of the Tote at the goal.
        /// No response needed.
        /// </summary>
        public event ToteLoadCompleteEventHandler ToteLoadComplete;

        /// <summary>
        /// Socket has completed the unloading of the Tote at the goal.
        /// No response needed.
        /// *The Job will complete a few seconds later.
        /// </summary>
        /// <param name="sender">This will always be "this".</param>
        /// <param name="data">Command #, Tote ID, Goal Name, Robot Number</param>
        public delegate void ToteUnloadCompleteEventHandler(object sender, SocketEventArgs data);
        /// <summary>
        /// Socket has completed the unloading of the Tote at the goal.
        /// No response needed.
        /// *The Job will complete a few seconds later.
        /// </summary>
        public event ToteUnloadCompleteEventHandler ToteUnloadComplete;

        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        public delegate void SocketEvent_IsSyncedUpdateEventHandler(object sender, bool data);
        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        private event SocketEvent_IsSyncedUpdateEventHandler SocketEvent_InSyncEvent;


        private delegate void SocketEvent_ProcessMessageEventHandler(object sender, SocketEventArgs data);
        private event SocketEvent_ProcessMessageEventHandler SocketEvent_ProcessMessageEvent;

        /// <summary>
        /// Set when calling Initialize().
        /// Bind IP Address for Socket Event Server:Port number to listen on.
        /// Initially string.Empty.
        /// </summary>
        public string SocketEvent_ConString { get; private set; } = string.Empty;

        public bool SocketEvent_InSync { get; private set; } = false;

        private SocketManager SocketEvent_Listener { get; set; }
        private SocketManager SocketEvent_Client { get; set; }

        public bool HealthCheckResponse(HealthCheckEventArgs response)
        {
            if (SocketEvent_Client.Write(response.GetCommandString())) return true;
            return false;
        }
        private bool RobotAtGoal(SocketEventArgs data)
        {
            if (SocketEvent_Client.Write(data.GetCommandString(11))) return true;
            return false;
        }

        //------------------------- Socket Event Comms
        private void SocketEvent_Restart()
        {
            SocketEvent_ListenerCleanup();

            SocketEvent_Listener = new SocketManager(SocketEvent_ConString);

            SocketEvent_Listener.ListenState += SocketEvent_Listener_ListenState; ;
            SocketEvent_Listener.Error += SocketEvent_Listener_Error;

            SocketEvent_Listener.Listen();
        }
        private void SocketEvent_Listener_ListenState(object sender, bool state)
        {
            if (state)
                SocketEvent_Listener.ListenClientConnected += SocketEvent_Listener_ListenClientConnected;
            else
                SocketEvent_Listener.ListenClientConnected -= SocketEvent_Listener_ListenClientConnected;
        }
        private void SocketEvent_Listener_ListenClientConnected(object sender, SocketManager.ListenClientConnectedEventArgs data)
        {
            if (SocketEvent_Client == null)
                SocketEvent_Client = new SocketManager(data.Client);

            SocketEvent_Client.DataReceived += SocketEvent_Client_DataReceived;
            SocketEvent_Client.Error += SocketEvent_Client_Error;

            SocketEvent_ProcessMessageEvent += SocketEvent_ProcessMessage;

            SocketEvent_Client.ReceiveAsync(MessageTerminatorString);
        }
        private void SocketEvent_Listener_Error(object sender, Exception data) => this.Queue(false, new Action(() => SocketEvent_Restart()));
        private void SocketEvent_ListenerCleanup()
        {
            if (SocketEvent_Listener != null)
            {
                SocketEvent_Listener.ListenState -= SocketEvent_Listener_ListenState;
                SocketEvent_Listener.ListenClientConnected -= SocketEvent_Listener_ListenClientConnected;
                SocketEvent_Listener.Error -= SocketEvent_Listener_Error;
            }

            SocketEvent_ClientCleanup();

            SocketEvent_Listener?.StopListen();
            SocketEvent_Listener?.Dispose();
            SocketEvent_Listener = null;
        }
        private void SocketEvent_Client_DataReceived(object sender, string data)
        {
            foreach (char c in MessageTrimChars)
                data.Trim(c);

            string[] spl = data.Split(MessageSplitChar);

            foreach (string s in spl)
            {
                if (string.IsNullOrEmpty(s)) continue;

                this.Queue(false, new Action(() => SocketEvent_ProcessMessageEvent?.Invoke(sender, new SocketEventArgs(s))));
            }
        }
        bool flipflop = true;
        private void SocketEvent_ProcessMessage(object sender, SocketEventArgs data)
        {
            if (!SocketEvent_InSync)
            {
                SocketEvent_InSync = true;
                this.Queue(false, new Action(() => SocketEvent_InSyncEvent?.Invoke(this, true)));
            }

            switch (data.Command)
            {
                case 1: //Initial Scan
                    this.Queue(false, new Action(() => HealthCheck?.Invoke(sender, new HealthCheckEventArgs(data.ID))));
                    HealthCheckEventArgs hc = new HealthCheckEventArgs(data.ID);
                    hc.Valid = flipflop;
                    flipflop ^= true;
                    HealthCheckResponse(hc);
                    break;
                case 10: //Tote ready for pickup.
                    this.Queue(false, new Action(() => ToteReady?.Invoke(sender, data)));
                    break;
                case 12: //Tote Loading complete.
                    this.Queue(false, new Action(() => ToteLoadComplete?.Invoke(sender, data)));
                    break;
                case 13: //Tote Unloading complete.
                    this.Queue(false, new Action(() => ToteUnloadComplete?.Invoke(sender, data)));
                    break;
            }
        }

        private void SocketEvent_Client_Error(object sender, Exception data) => this.Queue(false, new Action(() => SocketEvent_ClientCleanup()));

        private void SocketEvent_ClientCleanup()
        {
            SocketEvent_ProcessMessageEvent -= SocketEvent_ProcessMessage;

            if (SocketEvent_InSync)
            {
                SocketEvent_InSync = false;
                SocketEvent_InSyncEvent?.Invoke(this, false);
            }

            if (SocketEvent_Client == null) return;

            SocketEvent_Client?.Close();
            SocketEvent_Client?.Dispose();
            SocketEvent_Client = null;
        }

    }
}
