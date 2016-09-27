using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tc.TcMedia.Scheduler
{
    class Config
    {
        private long configId;
        private string name;
        private string type;
        private string avalue;

        public long ConfigId { get { return configId; } set { configId = value; } }
        public string Name { get { return name; } set { name = value; } }
        public string Type { get { return type; } set { type = value; } }
        public string Value { get { return avalue; } set { avalue = value; } }
        public object getValue
        {
            get
            {
                object ret = null;
                switch (type)
                {
                    case "String":
                        ret = avalue;
                        break;
                    case "Int":
                        ret = Convert.ToInt16(avalue);
                        break;
                    case "Long":
                        ret = Convert.ToInt32(avalue);
                        break;
                    case "DateTime":
                        ret = DateTime.Parse(avalue);
                        break;
                }
                return ret;
            }
        }
    }
}
