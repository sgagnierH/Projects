using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Tc.TcMedia.Scheduler
{
    public class Process
    {
        public Thread thread;
        public TabPage tabpage;
        public ListBox listbox;
        public Guid guid;
        public Schedule schedule;
        public Report report;
        public Db db;
        public MySql.Data.MySqlClient.MySqlConnection conn;
        public bool inDaySchedule = false;
        public bool justRun = false;
        public int group;

        public Process()
        {
            db = new Db(this);
            inDaySchedule = Db.inDaySchedule();
            conn = Db.getConnection();
        }
        public void clearLog()
        {
            MethodInvoker clearLog = delegate { listbox.Items.Clear(); };
            listbox.BeginInvoke(clearLog);
        }
        public void setTitle(string text, bool active = true)
        {
            if (tabpage == null)
                Console.WriteLine(text);
            else
            {
                try
                {
                    MethodInvoker setTitle = delegate { tabpage.Text = ((group == 99) ? "" : group.ToString()) + " [" + guid.ToString().Substring(34) + "] " + text; };
                    tabpage.BeginInvoke(setTitle);

                    Color col = Color.Gray;
                    if (active)
                        col = Color.White;

                    MethodInvoker setBack = delegate { tabpage.BackColor = col; };
                    tabpage.BeginInvoke(setBack);
                }
                catch (Exception) { }
            }
        }
        public void log(string text)
        {
            if (listbox == null)
                Console.Error.WriteLine(text);
            else
            {
                try
                {
                    text = System.DateTime.Now.ToString("HH:mm:ss : ") + text;
                    MethodInvoker doLog = delegate { listbox.Items.Add(text); };
                    listbox.Invoke(doLog);

                    int i = (int)listbox.Invoke(new Func<int>(delegate { return listbox.Items.Count; }));

                    MethodInvoker select = delegate { listbox.SelectedIndex = listbox.Items.Count - 1; };
                    listbox.Invoke(select);

                    while (i >= 500)
                    {
                        MethodInvoker remove = delegate { listbox.Items.RemoveAt(0); };
                        listbox.Invoke(remove);
                        i--;
                    }
                }
                catch (Exception) { }
            }
        }
    }
}
