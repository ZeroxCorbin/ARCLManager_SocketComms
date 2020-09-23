
#define TEST

using ARCLManager_SocketCommsNS;
using SocketManagerNS;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;


namespace Testing
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        ARCLManager_SocketComms ARCLManager_SocketComms { get; } = new ARCLManager_SocketComms();
        SocketManager IOSocket { get; set; }
        SocketManager EventSocket { get; set; }

        public MainWindow()
        {
            InitializeComponent();

            ARCLManager_SocketComms.HealthCheck += ARCLManager_SocketComms_HealthCheck;
            ARCLManager_SocketComms.ToteReady += ARCLManager_SocketComms_ToteReady;
            ARCLManager_SocketComms.ToteLoadComplete += ARCLManager_SocketComms_ToteLoadComplete;
            ARCLManager_SocketComms.ToteUnloadComplete += ARCLManager_SocketComms_ToteUnloadComplete;

#if TEST
            ARCLManager_SocketComms.Debug(ARCLManager_SocketComms_EmIOInSync, ARCLManager_SocketComms_EmRobotsInSync, ARCLManager_SocketComms_IOInSync, ARCLManager_SocketComms_EVInSync);
            ARCLManager_SocketComms.Initialize("192.168.0.20:7171:adept", "127.0.0.1:10001", "127.0.0.1:10000");
#else
            ARCLManager_SocketComms.Initialize("172.16.39.10:7171:adept", "172.16.39.9:10001", "172.16.39.9:10000");
#endif

            //ThreadPool.QueueUserWorkItem(new WaitCallback(BackgroundThread));
        }

        private void BackgroundThread(object sender)
        {
            while (true)
            {
                Thread.Sleep(1000);

                if (EventSocket != null)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)(() =>
                    {
                        BtnHealth_Click(new object(), new RoutedEventArgs());
                        BtnAtGoal_Click(new object(), new RoutedEventArgs());
                    }));
                }

                if (IOSocket != null)
                {
                    Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)(() =>
                    {
                        BtnInputs_Click(new object(), new RoutedEventArgs());
                        BtnOutputs_Click(new object(), new RoutedEventArgs());
                    }));
                }
            }
        }

        private void ARCLManager_SocketComms_EmIOInSync(object sender, bool data) => Dispatcher.BeginInvoke(DispatcherPriority.Render,
            (Action<bool>)((bool d) =>
            {
                if (d) LblEmIOInSync.Background = Brushes.LightGreen; else LblEmIOInSync.Background = Brushes.LightSalmon;

                txtEventList.Text += $"ExtIO Sync: {d}\r\n";
            }), data);
        private void ARCLManager_SocketComms_EmRobotsInSync(object sender, bool data) => Dispatcher.BeginInvoke(DispatcherPriority.Render,
            (Action<bool>)((bool d) => { if (d) LblEmRobotsInSync.Background = Brushes.LightGreen; else LblEmRobotsInSync.Background = Brushes.LightSalmon; txtEventList.Text += $"Robots Sync: {d}\r\n"; }), data);
        private void ARCLManager_SocketComms_IOInSync(object sender, bool data) => Dispatcher.BeginInvoke(DispatcherPriority.Render,
            (Action<bool>)((bool d) => { if (d) LblPLCIOInSync.Background = Brushes.LightGreen; else LblPLCIOInSync.Background = Brushes.LightSalmon; txtEventList.Text += $"PlcIO Sync: {d}\r\n"; }), data);
        private void ARCLManager_SocketComms_EVInSync(object sender, bool data) => Dispatcher.BeginInvoke(DispatcherPriority.Render,
            (Action<bool>)((bool d) => { if (d) LblPLCEventInSync.Background = Brushes.LightGreen; else LblPLCEventInSync.Background = Brushes.LightSalmon; txtEventList.Text += $"PlcEvent Sync: {d}\r\n"; }), data);

        private void ARCLManager_SocketComms_ToteUnloadComplete(object sender, SocketEventArgs data) => Dispatcher.BeginInvoke(DispatcherPriority.Render,
            (Action)(() => { txtEventList.Text += "Tote Loaded\r\n"; }));
        private void ARCLManager_SocketComms_ToteLoadComplete(object sender, SocketEventArgs data) => Dispatcher.BeginInvoke(DispatcherPriority.Render,
            (Action)(() => { txtEventList.Text += "Tote Unloaded\r\n"; }));
        private void ARCLManager_SocketComms_ToteReady(object sender, SocketEventArgs data) => Dispatcher.BeginInvoke(DispatcherPriority.Render,
            (Action)(() => { txtEventList.Text += "Tote Ready\r\n"; }));
        private void ARCLManager_SocketComms_HealthCheck(object sender, HealthCheckEventArgs data) => Dispatcher.BeginInvoke(DispatcherPriority.Render,
            (Action)(() => { txtEventList.Text += "Health Check\r\n"; }));

        private void BtnConnectIO_Click(object sender, RoutedEventArgs e)
        {
            if (IOSocket == null)
            {
                IOSocket = new SocketManager("127.0.0.1:10001");

                IOSocket.ConnectState += IOSocket_ConnectState;

                if (IOSocket.Connect())
                {
                    IOSocket.DataReceived += IOSocket_DataReceived;
                    IOSocket.StartReceiveAsync();
                }
            }
            else
            {
                IOSocket.StopReceiveAsync();
                IOSocket.Close();
            }
        }
        private void IOSocket_ConnectState(object sender, bool state)
        {
            if (state)
            {
                Dispatcher.Invoke(new Action(() => btnConnectIO.Background = Brushes.LightGreen));
            }
            else
            {
                IOSocket.DataReceived -= IOSocket_DataReceived;
                IOSocket.ConnectState -= IOSocket_ConnectState;
                IOSocket = null;
                Dispatcher.Invoke(new Action(() => btnConnectIO.Background = Brushes.LightSalmon));
            }
        }
        private void IOSocket_DataReceived(object sender, string data)
            => Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)(() => { txtIOResponse.Text = data; }));

        private void BtnConnectEvents_Click(object sender, RoutedEventArgs e)
        {
            if (EventSocket == null)
            {
                EventSocket = new SocketManager("127.0.0.1:10000");

                EventSocket.ConnectState += EventSocket_ConnectState;

                if (EventSocket.Connect())
                {
                    EventSocket.DataReceived += EventSocket_DataReceived;
                    EventSocket.StartReceiveAsync();
                }
            }
            else
            {
                EventSocket.StopReceiveAsync();
                EventSocket.Close();
            }
        }

        private void EventSocket_ConnectState(object sender, bool state)
        {
            if (state)
            {
                Dispatcher.Invoke(new Action(() => btnConnectEvents.Background = Brushes.LightGreen));
            }
            else
            {
                EventSocket.DataReceived -= EventSocket_DataReceived;
                EventSocket.ConnectState -= EventSocket_ConnectState;
                EventSocket = null;

                Dispatcher.Invoke(new Action(() => btnConnectEvents.Background = Brushes.LightSalmon));
            }
        }

        private void EventSocket_DataReceived(object sender, string data)
            => Dispatcher.BeginInvoke(DispatcherPriority.Render, (Action)(() => { txtEventResponse.Text = data; }));

        private void BtnInputs_Click(object sender, RoutedEventArgs e)
        {
            txtIOResponse.Text = "";
            IOSocket?.Write(txtInputs.Text + "\r\n");
        }
        private void BtnOutputs_Click(object sender, RoutedEventArgs e) => IOSocket?.Write(txtOutputs.Text + "\r\n");
        private void BtnHealth_Click(object sender, RoutedEventArgs e) => EventSocket?.Write(txtHealth.Text + "\r\n");
        private void BtnAtGoal_Click(object sender, RoutedEventArgs e) => EventSocket?.Write(txtAtGoal.Text + "\r\n");
        private void BtnLoaded_Click(object sender, RoutedEventArgs e) => EventSocket?.Write(txtLoaded.Text + "\r\n");
        private void BtnUnloaded_Click(object sender, RoutedEventArgs e) => EventSocket?.Write(txtUnloaded.Text + "\r\n");

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }
    }
}
