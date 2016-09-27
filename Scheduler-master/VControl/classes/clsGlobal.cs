using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Tc.TcMedia.Scheduler.Classes
{
    public sealed class clsGlobal
    {
        public static VControl g_VControl;
        public static Dictionary<Guid, Process> g_Processes;
        public static bool g_Running = true;
    }
}
