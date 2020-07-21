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
        /// Em IP Address:Port Number:ARCL Server Password
        /// Initially string.Empty.
        /// </summary>
        public string EmConString { get; private set; } = string.Empty;


        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        public delegate void EmIOInSyncUpdateEventHandler(object sender, bool data);
        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        private event EmIOInSyncUpdateEventHandler EmIO_InSync;

        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        public delegate void EmRobotsInSyncUpdateEventHandler(object sender, bool data);
        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        private event EmRobotsInSyncUpdateEventHandler EmRobots_InSync;
        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        public delegate void EmQueueInSyncUpdateEventHandler(object sender, bool data);
        /// <summary>
        /// Can only be attached calling Debug().
        /// </summary>
        private event EmQueueInSyncUpdateEventHandler EmQueue_InSync;

        //Private
        private ARCLConnection Connection { get; set; }

        private ExternalIOManager EmIO_Manager { get; set; }
        private QueueRobotManager EmRobots_Manager { get; set; }
        public QueueJobManager EmQueue_Manager { get; private set; }

        public bool EmIO_IsSynced => (EmIO_Manager == null) ? false : EmIO_Manager.IsSynced;
        public bool EmRobots_IsSynced => (EmRobots_Manager == null) ? false : EmRobots_Manager.IsSynced;
        public bool EmQueue_IsSynced => (EmQueue_Manager == null) ? false : EmQueue_Manager.IsSynced;

        public bool Em_RobotAvailable => (EmRobots_Manager == null) ? false : EmRobots_Manager.IsRobotAvailable;
        /// <summary>
        /// All systems lists IsSynced = EmIOIsSynced & EmRobotsIsSynced & IOIsSynced & EVIsSynced
        /// </summary>
        public bool IsSynced => EmIO_IsSynced & EmRobots_IsSynced & SocketIO_InSync & SocketEvent_InSync;

        /// <summary>
        /// Start the three main threads.
        /// Store the connection strings for reconnection attempts.
        /// </summary>
        /// <param name="emConString">Em IP Address:Port Number:ARCL Server Password</param>
        /// <param name="socketIOConString">Bind IP Address for Socket IO Server:Port number to listen on.</param>
        /// <param name="socketEventConString">Bind IP Address for Socket Event Server:Port number to listen on.</param>
        public void Initialize(string emConString, string socketIOConString, string socketEventConString)
        {
            EmConString = emConString;
            SocketIO_ConString = socketIOConString;
            SocketEvent_ConString = socketEventConString;

            this.Queue(true, new Action(() => SocketIO_Restart()));
            this.Queue(true, new Action(() => SocketEvent_Restart()));
            this.Queue(true, new Action(() => Em_Restart()));

            //ThreadPool.QueueUserWorkItem(new WaitCallback(SocketIO_Restart));
            //ThreadPool.QueueUserWorkItem(new WaitCallback(SocketEvent_Restart));
            //ThreadPool.QueueUserWorkItem(new WaitCallback(Em_Restart));
        }
        /// <summary>
        /// Shutdown all threads and active connections.
        /// </summary>
        public void Shutdown()
        {
            ConnectionCleanup();

            SocketEvent_ListenerCleanup();
            SocketIO_ListenerCleanup();
        }
        /// <summary>
        /// Response to the Socket from the HealthCheck event.
        /// </summary>
        /// <param name="response">HealthCheckEventArgs from the HealthCheck event, with the Valid bit set correctly.</param>
        /// <returns>The message was sent.</returns>

        public void Debug(EmIOInSyncUpdateEventHandler em_IOInSync, EmRobotsInSyncUpdateEventHandler em_RobotsInSync, IOInSyncUpdateEventHandler socketIO_InSyncEvent, SocketEvent_IsSyncedUpdateEventHandler socketEvent_InSyncEvent)
        {
            EmIO_InSync += em_IOInSync;
            EmRobots_InSync += em_RobotsInSync;
            SocketIO_InSyncEvent += socketIO_InSyncEvent;
            SocketEvent_InSyncEvent += socketEvent_InSyncEvent;
        }

        //------------------------- Em Comms
        private void Em_Restart()
        {
            Connection = new ARCLConnection(EmConString);
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

                EmIO_Manager = new ExternalIOManager(Connection, IOList);
                EmIO_Manager.InSync += EmIO_Manager_InSync;
                EmIO_Manager.Start();

                EmRobots_Manager = new QueueRobotManager(Connection);
                EmRobots_Manager.InSync += EmRobots_Manager_InSync;
                EmRobots_Manager.Start();

                EmQueue_Manager = new QueueJobManager(Connection);
                EmQueue_Manager.InSync += EmQueue_Manager_InSync;
                EmQueue_Manager.Start();
            }
            else
            {
                ConnectionCleanup();
                this.Queue(true, new Action(() => Em_Restart()));
            }
        }

        private void EmRobots_Manager_InSync(object sender, bool data) => EmRobots_InSync?.Invoke(sender, data);
        private void EmQueue_Manager_InSync(object sender, bool data) => EmQueue_InSync?.Invoke(sender, data);

        private void Connection_QueueUpdate(object sender, QueueManagerJobSegment data)
        {
            if (data.Status == ARCLStatus.InProgress)
            {
                if (data.SubStatus == ARCLSubStatus.AfterPickup)
                    RobotAtGoal(new SocketEventArgs(data.ID, data.GoalName, int.Parse(data.RobotName)));

                if (data.SubStatus == ARCLSubStatus.AfterDropoff)
                    RobotAtGoal(new SocketEventArgs(data.ID, data.GoalName, int.Parse(data.RobotName)));
            }
        }

        private void ConnectionCleanup()
        {
            if (Connection != null)
            {
                Connection.ConnectState -= Connection_ConnectState;
                Connection.QueueJobUpdate -= Connection_QueueUpdate;
            }

            if (EmRobots_Manager != null)
            {
                EmRobots_Manager.InSync -= EmRobots_Manager_InSync;
                EmRobots_Manager.Stop();
                EmRobots_Manager = null;
            }
            this.Queue(false, new Action(() => EmRobots_InSync?.Invoke(this, false)));

            if (EmIO_Manager != null)
            {
                EmIO_Manager.InSync -= EmIO_Manager_InSync;
                EmIO_Manager.Stop();
                
                EmIO_Manager = null;
            }
            this.Queue(false, new Action(() => EmIO_InSync?.Invoke(this, false)));

            if (EmQueue_Manager != null)
            {
                EmQueue_Manager.InSync -= EmQueue_Manager_InSync;
                EmQueue_Manager.Stop();

                EmQueue_Manager = null;
            }
            this.Queue(false, new Action(() => EmQueue_InSync?.Invoke(this, false)));

            Connection?.Close();
            Connection?.Dispose();
            Connection = null;
        }

    }
}
