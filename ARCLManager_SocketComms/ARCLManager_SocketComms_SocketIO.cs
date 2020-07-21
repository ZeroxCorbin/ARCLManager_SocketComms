using ARCL;
using ARCLTypes;
using SocketManagerNS;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ARCLManager_SocketCommsNS
{
    public partial class ARCLManager_SocketComms : GroupedTaskQueue
    {
        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        public delegate void IOInSyncUpdateEventHandler(object sender, bool data);
        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        private event IOInSyncUpdateEventHandler Socket_IOInSyncEvent;

        //Private Events
        private delegate void Socket_IOProcessMessageEventHandler(object sender, SocketIOArgs data);
        private event Socket_IOProcessMessageEventHandler Socket_IOProcessMessageEvent;

        private delegate void Socket_IOUpdateEMInputsEventHandler(SocketIOArgs data);
        private event Socket_IOUpdateEMInputsEventHandler Socket_IOUpdateEMInputsEvent;

        private delegate void Socket_IOUpdateEMOutputsEventHandler();
        private event Socket_IOUpdateEMOutputsEventHandler Socket_IOUpdateEMOutputsEvent;

        /// <summary>
        /// Set when calling Initialize().
        /// Bind IP Address for Socket IO Server:Port number to listen on.
        /// Initially string.Empty.
        /// </summary>
        public string Socket_IOConString { get; private set; } = string.Empty;

        public bool Socket_IOInSync { get; private set; } = false;

        private SocketManager Socket_IOListener { get; set; }
        private SocketManager Socket_IOClient { get; set; }


        //------------------------- Socket I/O Comms
        private void Socket_IORestart()
        {
            Socket_IOListenerCleanup();

            Socket_IOListener = new SocketManager(Socket_IOConString);

            Socket_IOListener.ListenState += Socket_IOListener_ListenState;
            Socket_IOListener.Error += Socket_IOListener_Error;

            Socket_IOListener.Listen();
        }
        private void Socket_IOListener_ListenState(object sender, bool state)
        {
            if (state)
                Socket_IOListener.ListenClientConnected += Socket_IOListener_ListenClientConnected;
            else
                Socket_IOListener.ListenClientConnected -= Socket_IOListener_ListenClientConnected;
        }
        private void Socket_IOListener_ListenClientConnected(object sender, SocketManager.ListenClientConnectedEventArgs data)
        {
            if (Socket_IOClient == null)
                Socket_IOClient = new SocketManager(data.Client);

            Socket_IOClient.DataReceived += Socket_IOClient_DataReceived;
            Socket_IOClient.Error += Socket_IOClient_Error;

            Socket_IOProcessMessageEvent += Socket_IOProcessMessage;
            Socket_IOUpdateEMInputsEvent += ARCLManager_SocketComms_Socket_IOUpdateEMInputsEvent;
            Socket_IOUpdateEMOutputsEvent += ARCLManager_SocketComms_Socket_IOUpdateEMOutputsEvent;

            Socket_IOClient.ReceiveAsync("\x03");
        }
        private void Socket_IOListener_Error(object sender, Exception data) => this.Queue(false, new Action(() => Socket_IORestart()));
        private void Socket_IOListenerCleanup()
        {
            if (Socket_IOListener != null)
            {
                Socket_IOListener.ListenState -= Socket_IOListener_ListenState;
                Socket_IOListener.Error -= Socket_IOListener_Error;
                Socket_IOListener.ListenClientConnected -= Socket_IOListener_ListenClientConnected;
            }

            Socket_IOClientCleanup();

            Socket_IOListener?.StopListen();
            Socket_IOListener?.Dispose();
            Socket_IOListener = null;
        }

        private bool WaitingForInputUpdate { get; set; } = false;
        private void ARCLManager_SocketComms_Socket_IOUpdateEMInputsEvent(SocketIOArgs data)
        {
            while (WaitingForOutputUpdate) { }
            WaitingForInputUpdate = EM_IOManager.WriteAllInputs(data.IO);
        }

        private bool WaitingForOutputUpdate { get; set; } = false;
        private void ARCLManager_SocketComms_Socket_IOUpdateEMOutputsEvent()
        {
            while (WaitingForInputUpdate) { }
             WaitingForOutputUpdate = EM_IOManager.ReadAllOutputs();
        }

        private void EM_IOManager_InSync(object sender, bool state)
        {
            EM_IOInSync?.Invoke(sender, state);

            if (Socket_IOClient == null) return;

            if (state)
            {
                List<byte> toSend = new List<byte>();

                if (WaitingForInputUpdate)
                {
                    for (int i = 1; i <= EM_IOManager.ActiveSets.Count(); i++)
                        toSend.AddRange(EM_IOManager.ActiveSets[i.ToString()].Inputs);

                    Socket_IOClient.Write(new SocketIOArgs(toSend).GetSocketCommandString(2));

                    WaitingForInputUpdate = false;

                    return;
                }

                if (WaitingForOutputUpdate)
                {
                    for (int i = 1; i <= EM_IOManager.ActiveSets.Count(); i++)
                        toSend.AddRange(EM_IOManager.ActiveSets[i.ToString()].Outputs);
                        
                    Socket_IOClient.Write(new SocketIOArgs(toSend).GetSocketCommandString(11));

                    WaitingForOutputUpdate = false;

                    return;
                }
            }
        }

        private void Socket_IOClient_DataReceived(object sender, string data)
        {
            string[] spl = data.Trim('\x02').Split('\x03');

            foreach (string s in spl)
            {
                if (string.IsNullOrEmpty(s)) continue;

                this.Queue(false, new Action(() => Socket_IOProcessMessageEvent?.Invoke(sender, new SocketIOArgs(s))));
            }
        }
        private void Socket_IOProcessMessage(object sender, SocketIOArgs data)
        {
            if (EM_IOManager == null)//Not connected to EM
            {
                Socket_IOClient.Write(data.GetSocketCommandString(data.Command + 2));
                return;
            }
            else if (!EM_IOManager.IsSynced)//Not ready for IO updates.
            {
                Socket_IOClient.Write(data.GetSocketCommandString(data.Command + 2));
                return;
            }

            if (!Socket_IOInSync)
            {
                Socket_IOInSync = true;
                this.Queue(false, new Action(() => Socket_IOInSyncEvent?.Invoke(this, true)));
            }

            switch (data.Command)
            {
                case 1: //Write Outputs
                    this.Queue(false, new Action(() => Socket_IOUpdateEMInputsEvent?.Invoke(data)));
                    break;
                case 10: //Read Inputs
                    this.Queue(false, new Action(() => Socket_IOUpdateEMOutputsEvent?.Invoke()));
                    break;
            }
        }

        private void Socket_IOClient_Error(object sender, Exception data) => this.Queue(false, new Action(() => Socket_IOClientCleanup()));
        private void Socket_IOClientCleanup()
        {
            Socket_IOProcessMessageEvent -= Socket_IOProcessMessage;
            Socket_IOUpdateEMInputsEvent -= ARCLManager_SocketComms_Socket_IOUpdateEMInputsEvent;
            Socket_IOUpdateEMOutputsEvent -= ARCLManager_SocketComms_Socket_IOUpdateEMOutputsEvent;

            if (Socket_IOInSync)
            {
                Socket_IOInSync = false;
                Socket_IOInSyncEvent?.Invoke(this, false);
            }

            if (Socket_IOClient == null) return;

            Socket_IOClient.DataReceived -= Socket_IOClient_DataReceived;
            Socket_IOClient.Error -= Socket_IOClient_Error;

            Socket_IOClient.Close();
            Socket_IOClient.Dispose();
            Socket_IOClient = null;
        }
    }
}
