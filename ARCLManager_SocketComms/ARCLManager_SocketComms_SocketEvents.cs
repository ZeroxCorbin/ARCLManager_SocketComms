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
        public delegate void Socket_EventIsSyncedUpdateEventHandler(object sender, bool data);
        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        private event Socket_EventIsSyncedUpdateEventHandler Socket_EventInSyncEvent;


        private delegate void Socket_EventProcessMessageEventHandler(object sender, SocketEventArgs data);
        private event Socket_EventProcessMessageEventHandler Socket_EventProcessMessageEvent;

        /// <summary>
        /// Set when calling Initialize().
        /// Bind IP Address for Socket Event Server:Port number to listen on.
        /// Initially string.Empty.
        /// </summary>
        public string Socket_EventConString { get; private set; } = string.Empty;

        public bool Socket_EventInSync { get; private set; } = false;

        private SocketManager Socket_EventListener { get; set; }
        private SocketManager Socket_EventClient { get; set; }

        public bool HealthCheckResponse(HealthCheckEventArgs response)
        {
            if (Socket_EventClient.Write(response.GetCommandString())) return true;
            return false;
        }
        private bool RobotAtGoal(SocketEventArgs data)
        {
            if (Socket_EventClient.Write(data.GetCommandString(11))) return true;
            return false;
        }

        //------------------------- Socket Event Comms
        private void Socket_EventRestart()
        {
            Socket_EventListenerCleanup();

            Socket_EventListener = new SocketManager(Socket_EventConString);

            Socket_EventListener.ListenState += Socket_EventListener_ListenState; ;
            Socket_EventListener.Error += Socket_EventListener_Error;

            Socket_EventListener.Listen();
        }
        private void Socket_EventListener_ListenState(object sender, bool state)
        {
            if (state)
                Socket_EventListener.ListenClientConnected += Socket_EventListener_ListenClientConnected;
            else
                Socket_EventListener.ListenClientConnected -= Socket_EventListener_ListenClientConnected;
        }
        private void Socket_EventListener_ListenClientConnected(object sender, SocketManager.ListenClientConnectedEventArgs data)
        {
            if (Socket_EventClient == null)
                Socket_EventClient = new SocketManager(data.Client);

            Socket_EventClient.DataReceived += Socket_EventClient_DataReceived;
            Socket_EventClient.Error += Socket_EventClient_Error;

            Socket_EventProcessMessageEvent += Socket_EventProcessMessage;

            Socket_EventClient.ReceiveAsync(MessageTerminatorString);
        }
        private void Socket_EventListener_Error(object sender, Exception data) => this.Queue(false, new Action(() => Socket_EventRestart()));
        private void Socket_EventListenerCleanup()
        {
            if (Socket_EventListener != null)
            {
                Socket_EventListener.ListenState -= Socket_EventListener_ListenState;
                Socket_EventListener.ListenClientConnected -= Socket_EventListener_ListenClientConnected;
                Socket_EventListener.Error -= Socket_EventListener_Error;
            }

            Socket_EventClientCleanup();

            Socket_EventListener?.StopListen();
            Socket_EventListener?.Dispose();
            Socket_EventListener = null;
        }
        private void Socket_EventClient_DataReceived(object sender, string data)
        {
            foreach (char c in MessageTrimChars)
                data.Trim(c);

            string[] spl = data.Split(MessageSplitChar);

            foreach (string s in spl)
            {
                if (string.IsNullOrEmpty(s)) continue;

                this.Queue(false, new Action(() => Socket_EventProcessMessageEvent?.Invoke(sender, new SocketEventArgs(s))));
            }
        }
        bool flipflop = true;
        private void Socket_EventProcessMessage(object sender, SocketEventArgs data)
        {
            if (!Socket_EventInSync)
            {
                Socket_EventInSync = true;
                this.Queue(false, new Action(() => Socket_EventInSyncEvent?.Invoke(this, true)));
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

        private void Socket_EventClient_Error(object sender, Exception data) => this.Queue(false, new Action(() => Socket_EventClientCleanup()));

        private void Socket_EventClientCleanup()
        {
            Socket_EventProcessMessageEvent -= Socket_EventProcessMessage;

            if (Socket_EventInSync)
            {
                Socket_EventInSync = false;
                Socket_EventInSyncEvent?.Invoke(this, false);
            }

            if (Socket_EventClient == null) return;

            Socket_EventClient?.Close();
            Socket_EventClient?.Dispose();
            Socket_EventClient = null;
        }

    }
}
