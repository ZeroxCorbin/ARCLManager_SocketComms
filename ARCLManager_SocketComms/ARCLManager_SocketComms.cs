using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using ARCL;
using ARCLTypes;
using SocketManagerNS;

namespace ARCLManager_SocketCommsNS
{
    public partial class ARCLManager_SocketComms : GroupedTaskQueue
    {
        /// <summary>
        /// Set when calling Initialize().
        /// EM IP Address:Port Number:ARCL Server Password
        /// Initially string.Empty.
        /// </summary>
        public string EMConString { get; private set; } = string.Empty;


        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        public delegate void EMIOInSyncUpdateEventHandler(object sender, bool data);
        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        private event EMIOInSyncUpdateEventHandler EM_IOInSync;

        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        public delegate void EMRobotsInSyncUpdateEventHandler(object sender, bool data);
        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        private event EMRobotsInSyncUpdateEventHandler EM_RobotsInSync;
        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        public delegate void EMQueueInSyncUpdateEventHandler(object sender, bool data);
        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        private event EMQueueInSyncUpdateEventHandler EM_QueueInSync;

        //Private
        private ARCLConnection Connection { get; set; }

        private ExternalIOManager EM_IOManager { get; set; }
        private QueueRobotManager EM_RobotsManager { get; set; }
        public QueueJobManager EM_QueueManager { get; private set; }

        public bool EM_IOIsSynced => (EM_IOManager == null) ? false : EM_IOManager.IsSynced;
        public bool EM_RobotsIsSynced => (EM_RobotsManager == null) ? false : EM_RobotsManager.IsSynced;
        public bool EM_QueueIsSynced => (EM_QueueManager == null) ? false : EM_QueueManager.IsSynced;

        public bool EM_RobotAvailable => (EM_RobotsManager == null) ? false : EM_RobotsManager.IsRobotAvailable;
        /// <summary>
        /// All systems lists IsSynced = EMIOIsSynced & EMRobotsIsSynced & IOIsSynced & EVIsSynced
        /// </summary>
        public bool IsSynced => EM_IOIsSynced & EM_RobotsIsSynced & Socket_IOInSync & Socket_EventInSync;

        /// <summary>
        /// Start the three main threads.
        /// Store the connection strings for reconnection attempts.
        /// </summary>
        /// <param name="emConString">EM IP Address:Port Number:ARCL Server Password</param>
        /// <param name="plcIOConString">Bind IP Address for Socket IO Server:Port number to listen on.</param>
        /// <param name="plcEventConString">Bind IP Address for Socket Event Server:Port number to listen on.</param>
        public void Initialize(string emConString, string plcIOConString, string plcEventConString)
        {
            EMConString = emConString;
            Socket_IOConString = plcIOConString;
            Socket_EventConString = plcEventConString;

            this.Queue(true, new Action(() => Socket_IORestart()));
            this.Queue(true, new Action(() => Socket_EventRestart()));
            this.Queue(true, new Action(() => EM_Restart()));

            //ThreadPool.QueueUserWorkItem(new WaitCallback(Socket_IORestart));
            //ThreadPool.QueueUserWorkItem(new WaitCallback(Socket_EventRestart));
            //ThreadPool.QueueUserWorkItem(new WaitCallback(EM_Restart));
        }
        /// <summary>
        /// Shutdown all threads and active connections.
        /// </summary>
        public void Shutdown()
        {
            ConnectionCleanup();

            Socket_EventListenerCleanup();
            Socket_IOListenerCleanup();
        }
        /// <summary>
        /// Response to the Socket from the HealthCheck event.
        /// </summary>
        /// <param name="response">HealthCheckEventArgs from the HealthCheck event, with the Valid bit set correctly.</param>
        /// <returns>The message was sent.</returns>

        public void Debug(EMIOInSyncUpdateEventHandler eM_IOInSync, EMRobotsInSyncUpdateEventHandler eM_RobotsInSync, IOInSyncUpdateEventHandler pLC_IOInSyncEvent, Socket_EventIsSyncedUpdateEventHandler pLC_EventInSyncEvent)
        {
            EM_IOInSync += eM_IOInSync;
            EM_RobotsInSync += eM_RobotsInSync;
            Socket_IOInSyncEvent += pLC_IOInSyncEvent;
            Socket_EventInSyncEvent += pLC_EventInSyncEvent;
        }

        //------------------------- EM Comms
        private void EM_Restart()
        {
            Connection = new ARCLConnection(EMConString);
            Connection.ConnectState += Connection_ConnectState;
            Connection.Connect();
        }

        private void Connection_ConnectState(object sender, bool state)
        {
            if (state)
            {
                Connection.QueueJobUpdate += Connection_QueueUpdate;

                Dictionary<string, ExtIOSet> IOList = new Dictionary<string, ExtIOSet>()
                    {
                        {"1", new ExtIOSet("1", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"2", new ExtIOSet("2", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"3", new ExtIOSet("3", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"4", new ExtIOSet("4", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"5", new ExtIOSet("5", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"6", new ExtIOSet("6", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"7", new ExtIOSet("7", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"8", new ExtIOSet("8", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"9", new ExtIOSet("9", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"10", new ExtIOSet("10", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"11", new ExtIOSet("11", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"12", new ExtIOSet("12", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"13", new ExtIOSet("13", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"14", new ExtIOSet("14", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"15", new ExtIOSet("15", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"16", new ExtIOSet("16", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"17", new ExtIOSet("17", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"18", new ExtIOSet("18", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"19", new ExtIOSet("19", new List<byte>{ 0 }, new List<byte>{ 0 }) },
                        {"20", new ExtIOSet("20", new List<byte>{ 0 }, new List<byte>{ 0 }) }
                    };

                EM_IOManager = new ExternalIOManager(Connection, IOList);
                EM_IOManager.InSync += EM_IOManager_InSync;
                EM_IOManager.Start();

                EM_RobotsManager = new QueueRobotManager(Connection);
                EM_RobotsManager.InSync += EM_RobotsManager_InSync;
                EM_RobotsManager.Start();

                EM_QueueManager = new QueueJobManager(Connection);
                EM_QueueManager.InSync += EM_QueueManager_InSync;
                EM_QueueManager.Start();
            }
            else
            {
                ConnectionCleanup();
                this.Queue(true, new Action(() => EM_Restart()));
            }
        }

        private void EM_RobotsManager_InSync(object sender, bool data) => EM_RobotsInSync?.Invoke(sender, data);
        private void EM_QueueManager_InSync(object sender, bool data) => EM_QueueInSync?.Invoke(sender, data);

        private void Connection_QueueUpdate(object sender, QueueManagerJobSegment data)
        {
            if (data.Status == ARCLStatus.InProgress)
            {
                if (data.SubStatus == ARCLSubStatus.AfterPickup)
                {


#if PARK
                    SocketEventArgs args = new SocketEventArgs(data.ID, data.GoalName, int.Parse(data.RobotName));
                    RobotAtGoal(args);
                    this.Queue(false, new Action(() => ToteLoadComplete?.Invoke(this, args)));
#else
                    RobotAtGoal(new SocketEventArgs(data.ID, data.GoalName, int.Parse(data.RobotName)));
#endif
                }


                if (data.SubStatus == ARCLSubStatus.AfterDropoff)
                {
#if PARK
                    SocketEventArgs args = new SocketEventArgs(data.ID, data.GoalName, int.Parse(data.RobotName));
                    RobotAtGoal(args);
                    this.Queue(false, new Action(() => ToteUnloadComplete?.Invoke(this, args)));
#else
                    RobotAtGoal(new SocketEventArgs(data.ID, data.GoalName, int.Parse(data.RobotName)));
#endif
                }

            }
        }

        private void ConnectionCleanup()
        {
            if (Connection != null)
            {
                Connection.ConnectState -= Connection_ConnectState;
                Connection.QueueJobUpdate -= Connection_QueueUpdate;
            }

            if (EM_RobotsManager != null)
            {
                EM_RobotsManager.InSync -= EM_RobotsManager_InSync;
                EM_RobotsManager.Stop();
                EM_RobotsManager = null;
            }
            this.Queue(false, new Action(() => EM_RobotsInSync?.Invoke(this, false)));

            if (EM_IOManager != null)
            {
                EM_IOManager.InSync -= EM_IOManager_InSync;
                EM_IOManager.Stop();
                
                EM_IOManager = null;
            }
            this.Queue(false, new Action(() => EM_IOInSync?.Invoke(this, false)));

            if (EM_QueueManager != null)
            {
                EM_QueueManager.InSync -= EM_QueueManager_InSync;
                EM_QueueManager.Stop();

                EM_QueueManager = null;
            }
            this.Queue(false, new Action(() => EM_QueueInSync?.Invoke(this, false)));

            Connection?.Close();
            Connection?.Dispose();
            Connection = null;
        }

    }
}
