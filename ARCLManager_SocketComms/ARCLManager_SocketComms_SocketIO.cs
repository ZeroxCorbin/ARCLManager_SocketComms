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
        private event IOInSyncUpdateEventHandler SocketIO_InSyncEvent;

        //Private Events
        private delegate void SocketIO_ProcessMessageEventHandler(object sender, SocketIOArgs data);
        private event SocketIO_ProcessMessageEventHandler SocketIO_ProcessMessageEvent;

        private delegate void SocketIO_UpdateEmInputsEventHandler(SocketIOArgs data);
        private event SocketIO_UpdateEmInputsEventHandler SocketIO_UpdateEmInputsEvent;

        private delegate void SocketIO_UpdateEmOutputsEventHandler();
        private event SocketIO_UpdateEmOutputsEventHandler SocketIO_UpdateEmOutputsEvent;

        /// <summary>
        /// Set when calling Initialize().
        /// Bind IP Address for Socket IO Server:Port number to listen on.
        /// Initially string.Empty.
        /// </summary>
        public string SocketIO_ConString { get; private set; } = string.Empty;

        public bool SocketIO_InSync { get; private set; } = false;

        private SocketManager SocketIO_Listener { get; set; }
        private SocketManager SocketIO_Client { get; set; }


        //------------------------- Socket I/O Comms
        private void SocketIO_Restart()
        {
            SocketIO_ListenerCleanup();

            SocketIO_Listener = new SocketManager(SocketIO_ConString);

            SocketIO_Listener.ListenState += SocketIO_Listener_ListenState;
            SocketIO_Listener.Error += SocketIO_Listener_Error;

            SocketIO_Listener.Listen();
        }
        private void SocketIO_Listener_ListenState(object sender, bool state)
        {
            if (state)
                SocketIO_Listener.ListenClientConnected += SocketIO_Listener_ListenClientConnected;
            else
                SocketIO_Listener.ListenClientConnected -= SocketIO_Listener_ListenClientConnected;
        }
        private void SocketIO_Listener_ListenClientConnected(object sender, SocketManager.ListenClientConnectedEventArgs data)
        {
            if (SocketIO_Client == null)
                SocketIO_Client = new SocketManager(data.Client);

            SocketIO_Client.DataReceived += SocketIO_Client_DataReceived;
            SocketIO_Client.Error += SocketIO_Client_Error;

            SocketIO_ProcessMessageEvent += SocketIO_ProcessMessage;
            SocketIO_UpdateEmInputsEvent += ARCLManager_SocketComms_SocketIO_UpdateEmInputsEvent;
            SocketIO_UpdateEmOutputsEvent += ARCLManager_SocketComms_SocketIO_UpdateEmOutputsEvent;

            SocketIO_Client.StartReceiveAsync("\x03");
        }
        private void SocketIO_Listener_Error(object sender, Exception data) => this.Queue(false, new Action(() => SocketIO_Restart()));
        private void SocketIO_ListenerCleanup()
        {
            if (SocketIO_Listener != null)
            {
                SocketIO_Listener.ListenState -= SocketIO_Listener_ListenState;
                SocketIO_Listener.Error -= SocketIO_Listener_Error;
                SocketIO_Listener.ListenClientConnected -= SocketIO_Listener_ListenClientConnected;
            }

            SocketIO_ClientCleanup();

            SocketIO_Listener?.StopListen();
            SocketIO_Listener?.Dispose();
            SocketIO_Listener = null;
        }

        private bool WaitingForInputUpdate { get; set; } = false;
        private void ARCLManager_SocketComms_SocketIO_UpdateEmInputsEvent(SocketIOArgs data)
        {
            for (int i = 1; i <= EmIO_Manager.ActiveSets.Count(); i++)
                EmIO_Manager.ActiveSets[i.ToString()].Inputs = new List<byte>() { data.IO[i - 1] };

            while (WaitingForOutputUpdate) { }

            WaitingForInputUpdate = EmIO_Manager.WriteAllInputs();
        }

        private bool WaitingForOutputUpdate { get; set; } = false;
        private void ARCLManager_SocketComms_SocketIO_UpdateEmOutputsEvent()
        {
            while (WaitingForInputUpdate) { }
             WaitingForOutputUpdate = EmIO_Manager.ReadAllOutputs();
        }

        private void EmIO_Manager_InSync(object sender, bool state)
        {
            EmIO_InSync?.Invoke(sender, state);

            if (SocketIO_Client == null) return;

            if (state)
            {
                List<byte> toSend = new List<byte>();

                if (WaitingForInputUpdate)
                {
                    for (int i = 1; i <= EmIO_Manager.ActiveSets.Count(); i++)
                        toSend.AddRange(EmIO_Manager.ActiveSets[i.ToString()].Inputs);

                    SocketIO_Client.Write(new SocketIOArgs(toSend).GetSocketCommandString(2));

                    WaitingForInputUpdate = false;

                    return;
                }

                if (WaitingForOutputUpdate)
                {
                    for (int i = 1; i <= EmIO_Manager.ActiveSets.Count(); i++)
                        toSend.AddRange(EmIO_Manager.ActiveSets[i.ToString()].Outputs);
                        
                    SocketIO_Client.Write(new SocketIOArgs(toSend).GetSocketCommandString(11));

                    WaitingForOutputUpdate = false;

                    return;
                }
            }
        }

        private void SocketIO_Client_DataReceived(object sender, string data)
        {
            string[] spl = data.Trim('\x02').Split('\x03');

            foreach (string s in spl)
            {
                if (string.IsNullOrEmpty(s)) continue;

                this.Queue(false, new Action(() => SocketIO_ProcessMessageEvent?.Invoke(sender, new SocketIOArgs(s))));
            }
        }
        private void SocketIO_ProcessMessage(object sender, SocketIOArgs data)
        {
            if (EmIO_Manager == null)//Not connected to Em
            {
                SocketIO_Client.Write(data.GetSocketCommandString(data.Command + 2));
                return;
            }
            else if (!EmIO_Manager.IsSynced)//Not ready for IO updates.
            {
                SocketIO_Client.Write(data.GetSocketCommandString(data.Command + 2));
                return;
            }

            if (!SocketIO_InSync)
            {
                SocketIO_InSync = true;
                this.Queue(false, new Action(() => SocketIO_InSyncEvent?.Invoke(this, true)));
            }

            switch (data.Command)
            {
                case 1: //Write Outputs
                    this.Queue(false, new Action(() => SocketIO_UpdateEmInputsEvent?.Invoke(data)));
                    break;
                case 10: //Read Inputs
                    this.Queue(false, new Action(() => SocketIO_UpdateEmOutputsEvent?.Invoke()));
                    break;
            }
        }

        private void SocketIO_Client_Error(object sender, Exception data) => this.Queue(false, new Action(() => SocketIO_ClientCleanup()));
        private void SocketIO_ClientCleanup()
        {
            SocketIO_ProcessMessageEvent -= SocketIO_ProcessMessage;
            SocketIO_UpdateEmInputsEvent -= ARCLManager_SocketComms_SocketIO_UpdateEmInputsEvent;
            SocketIO_UpdateEmOutputsEvent -= ARCLManager_SocketComms_SocketIO_UpdateEmOutputsEvent;

            if (SocketIO_InSync)
            {
                SocketIO_InSync = false;
                SocketIO_InSyncEvent?.Invoke(this, false);
            }

            if (SocketIO_Client == null) return;

            SocketIO_Client.DataReceived -= SocketIO_Client_DataReceived;
            SocketIO_Client.Error -= SocketIO_Client_Error;

            SocketIO_Client.Close();
            SocketIO_Client.Dispose();
            SocketIO_Client = null;
        }
    }
}
