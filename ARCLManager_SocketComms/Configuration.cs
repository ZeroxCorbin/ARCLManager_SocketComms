using System;
using System.Collections.Generic;
using System.Text;

namespace ARCLManager_SocketCommsNS
{
    public partial class ARCLManager_SocketComms
    {
        private string MessageTerminatorString { get; } = "\x03";
        private List<char> MessageTrimChars { get; } = new List<char>() { '\x02' };
        private char MessageSplitChar { get; } = '\x03';

    }
}
