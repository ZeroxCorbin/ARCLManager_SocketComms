using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ARCLManager_SocketCommsNS
{
    public class HealthCheckEventArgs : EventArgs
    {
        private const string delimeter = ",";
        public string ID { get; }
        public bool Valid { get; set; } = false;

        public HealthCheckEventArgs(string id)
        {
            ID = id;
        }

        public string GetCommandString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append('\x02');
            if (Valid)
                sb.Append("2"); //Return Valid Tote
            else
                sb.Append("3"); //Return Invalid Tote

            sb.Append(delimeter);
            sb.Append(ID);
            sb.Append(delimeter);
            sb.Append("");
            sb.Append(delimeter);
            sb.Append("0");
            sb.Append("\x03");

            return sb.ToString();
        }

    }

    public class SocketEventArgs : EventArgs
    {
        private const char delimeter = ',';

        public int Command { get; }
        public string ID { get; }
        public string GoalName { get; }
        public int RobotNumber { get; }

        public SocketEventArgs(string msg)
        {
            string[] spl = msg.Split(delimeter);

            Command = int.Parse(spl[0]);
            ID = spl[1];
            GoalName = spl[2];
            RobotNumber = int.Parse(spl[3]);
        }

        public SocketEventArgs(string id, string goalName, int robotNumber = 0)
        {
            ID = id;
            GoalName = goalName;
            RobotNumber = robotNumber;
        }

        public string GetCommandString(int commandNumber)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append('\x02');
            sb.Append(commandNumber.ToString());
            sb.Append(delimeter);
            sb.Append(ID);
            sb.Append(delimeter);
            sb.Append(GoalName);
            sb.Append(delimeter);
            sb.Append(RobotNumber.ToString());
            sb.Append("\x03");

            return sb.ToString();
        }
    }

    public class SocketIOArgs : EventArgs
    {
        private const char delimeter = ',';

        public int Command { get; }
        public List<byte> IO { get; private set; } = new List<byte>();

        public SocketIOArgs(string str)
        {
            string[] spl = str.Split(delimeter);

            Command = int.Parse(spl[0]);

            for(int i = 0; ; i += 8)
            {
                if (i + 8 > spl[1].Length) break;
                string temp = spl[1].Substring(i, 8);
                IO.Add(Convert.ToByte(temp, 2));
            }
        }
        public SocketIOArgs(List<byte> io) => IO = io;

        public string GetSocketCommandString(int commandNumber)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append('\x02');
            sb.Append(commandNumber.ToString());
            sb.Append(delimeter);

            foreach(byte b in IO)
            {
                string temp = Convert.ToString(b, 2);
                if (8 - temp.Count() > 0)
                    temp = temp.PadLeft(9 - temp.Count(), '0');
                sb.Append(temp);
            }

            sb.Append("\x03");

            return sb.ToString();
        }
    }

}
